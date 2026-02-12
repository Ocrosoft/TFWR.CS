using TFWR.CS.Transpiler;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// ── 路径配置 ──────────────────────────────────────────────────────────────────
// 可执行文件所在目录（如 Save0/TFWR.CS/）
var exeDir = AppContext.BaseDirectory;

// 源码目录：exe 的同级 cs 目录（如 Save0/cs/）
// 输出目录：exe 的父目录（如 Save0/）
string csDir;
string outputDir;

// 如果是调试模式（bin/Debug/net9.0），使用游戏的 Save0 目录
if (exeDir.Contains("bin"))
{
    var save0 = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "TheFarmerWasReplaced", "TheFarmerWasReplaced", "Saves", "Save0");

    if (Directory.Exists(save0))
    {
        outputDir = save0;
        csDir = Path.Combine(save0, "cs");
        Console.WriteLine("调试模式：使用游戏 Save0 目录");
    }
    else
    {
        outputDir = exeDir;
        csDir = Path.Combine(exeDir, "cs");
    }
}
else
{
    // 正常运行：exe 在 Save0/TFWR.CS/ 下
    outputDir = Path.GetFullPath(Path.Combine(exeDir, ".."));
    csDir = Path.GetFullPath(Path.Combine(exeDir, "..", "cs"));
}

// 确保 cs 目录存在
if (!Directory.Exists(csDir))
{
    Console.Error.WriteLine($"[错误] 源码目录不存在: {csDir}");
    Console.Error.WriteLine("请在此目录下创建 .cs 文件");
    Directory.CreateDirectory(csDir);
}

Console.WriteLine($"源码目录: {csDir}");
Console.WriteLine($"输出目录: {outputDir}");
Console.WriteLine($"监控 cs/*.cs 文件并转译为 *.py");
Console.WriteLine("按 Ctrl+C 退出");
Console.WriteLine(new string('─', 60));

// ── 启动时先全量转译一次 ──────────────────────────────────────────────────────
foreach (var file in Directory.EnumerateFiles(csDir, "*.cs"))
{
    TranspileFile(file);
}

// ── 文件监控 ──────────────────────────────────────────────────────────────────
using var watcher = new FileSystemWatcher(csDir, "*.cs")
{
    NotifyFilter = NotifyFilters.FileName
     | NotifyFilters.LastWrite
        | NotifyFilters.CreationTime,
    IncludeSubdirectories = false,
    EnableRaisingEvents = true
};

watcher.Changed += OnChanged;
watcher.Created += OnChanged;
watcher.Renamed += OnRenamed;
watcher.Deleted += OnDeleted;

// 保持进程运行直到 Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (TaskCanceledException)
{
    // 正常退出
}

Console.WriteLine("\n已停止监控。");

// ── 事件处理 ──────────────────────────────────────────────────────────────────

void OnChanged(object sender, FileSystemEventArgs e)
{
    if (ShouldIgnoreFile(e.FullPath))
        return;

    Task.Delay(200).ContinueWith(_ => TranspileFile(e.FullPath));
}

void OnRenamed(object sender, RenamedEventArgs e)
{
    if (ShouldIgnoreFile(e.FullPath))
        return;

    // 删除旧的 .py
    var oldPy = GetOutputPath(e.OldFullPath);
    if (File.Exists(oldPy))
    {
        File.Delete(oldPy);
        Console.WriteLine($"  删除: {Path.GetFileName(oldPy)}");
    }

    Task.Delay(200).ContinueWith(_ => TranspileFile(e.FullPath));
}

void OnDeleted(object sender, FileSystemEventArgs e)
{
    if (ShouldIgnoreFile(e.FullPath))
        return;

    var pyPath = GetOutputPath(e.FullPath);
    if (File.Exists(pyPath))
    {
        File.Delete(pyPath);
        Console.WriteLine($"  删除: {Path.GetFileName(pyPath)}");
    }
}

// ── 转译逻辑 ──────────────────────────────────────────────────────────────────

void TranspileFile(string csPath)
{
    if (ShouldIgnoreFile(csPath))
        return;

    if (!File.Exists(csPath))
    {
        Console.WriteLine($"[跳过] 文件不存在: {Path.GetFileName(csPath)}");
        return;
    }

    var fileName = Path.GetFileName(csPath);
    var pyPath = GetOutputPath(csPath);

    var nameNoExt = Path.GetFileNameWithoutExtension(csPath);
    var isEntry = nameNoExt.Equals("Program", StringComparison.OrdinalIgnoreCase);

    // 扫描 cs 目录中的所有 .cs 文件，建立类名映射
    var classToFileMap = BuildClassToFileMap(csDir);

    try
    {
        string source = ReadFileWithRetry(csPath);
        var result = CSharpToTfwrTranspiler.Transpile(
 source,
      isEntryFile: isEntry,
   classToFileMap: classToFileMap,
            currentFileName: nameNoExt);
        File.WriteAllText(pyPath, result);

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] cs/{fileName} → {Path.GetFileName(pyPath)}{(isEntry ? " (入口)" : "")}");
    }
    catch (FileNotFoundException)
    {
        Console.WriteLine($"[跳过] 文件已消失: {fileName}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[错误] {fileName}: {ex.Message}");
    }
}

/// <summary>
/// 扫描源码目录中的所有 .cs 文件，建立类名到文件名的映射
/// </summary>
Dictionary<string, string> BuildClassToFileMap(string directory)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);

    foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
    {
        if (ShouldIgnoreFile(file))
            continue;

        try
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source);
            var root = tree.GetCompilationUnitRoot();

            var classes = root.DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
             .Select(c => c.Identifier.Text);

            var fileNameNoExt = Path.GetFileNameWithoutExtension(file);

            string mappedFileName;
            if (fileNameNoExt.Equals("Program", StringComparison.OrdinalIgnoreCase))
            {
                mappedFileName = "main";
            }
            else
            {
                mappedFileName = ConvertToSnakeCase(fileNameNoExt);
            }

            foreach (var className in classes)
            {
                if (map.ContainsKey(className))
                {
                    Console.WriteLine($"[警告] 类 '{className}' 在多个文件中定义: {map[className]}.py 和 {mappedFileName}.py");
                }
                map[className] = mappedFileName;
            }
        }
        catch
        {
            // 忽略无法解析的文件
        }
    }

    return map;
}

/// <summary>
/// 获取 .cs 文件对应的 .py 输出路径（输出到 outputDir）
/// </summary>
string GetOutputPath(string csPath)
{
    var name = Path.GetFileNameWithoutExtension(csPath);

    // Program.cs → main.py
    if (name.Equals("Program", StringComparison.OrdinalIgnoreCase))
        return Path.Combine(outputDir, "main.py");

    var snakeCaseName = ConvertToSnakeCase(name);
    return Path.Combine(outputDir, snakeCaseName + ".py");
}

/// <summary>
/// 将 PascalCase 或 camelCase 转换为 snake_case
/// </summary>
string ConvertToSnakeCase(string name)
{
    if (string.IsNullOrEmpty(name)) return name;

    if (name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c)))
        return name.ToLowerInvariant();

    if (name.Contains('_'))
        return name.ToLowerInvariant();

    var sb = new System.Text.StringBuilder();
    for (int i = 0; i < name.Length; i++)
    {
        var c = name[i];
        if (char.IsUpper(c) && i > 0)
        {
            bool prevUpper = char.IsUpper(name[i - 1]);
            bool nextLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
            if (!prevUpper || nextLower)
                sb.Append('_');
        }
        sb.Append(char.ToLowerInvariant(c));
    }
    return sb.ToString();
}

string ReadFileWithRetry(string path, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException) when (i < maxRetries - 1)
        {
            Thread.Sleep(100);
        }
    }
    return File.ReadAllText(path);
}

// ── 辅助方法 ──────────────────────────────────────────────────────────────────

bool ShouldIgnoreFile(string path)
{
    if (Path.GetExtension(path) != ".cs")
        return true;

    return false;
}
