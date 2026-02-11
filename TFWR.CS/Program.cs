using TFWR.CS.Transpiler;

// ── 路径配置 ──────────────────────────────────────────────────────────────────
// 使用可执行文件所在目录作为工作目录
var workingDir = AppContext.BaseDirectory;

// 如果是调试模式（bin/Debug/net9.0），使用游戏的 Save0 目录
if (workingDir.Contains("bin"))
{
    var save0 = Path.Combine(
   Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "TheFarmerWasReplaced", "TheFarmerWasReplaced", "Saves", "Save0");

    if (Directory.Exists(save0))
    {
        workingDir = save0;
        Console.WriteLine("调试模式：使用游戏 Save0 目录");
    }
}

Console.WriteLine($"工作目录: {workingDir}");
Console.WriteLine($"监控 .cs 文件并转译为 .py");
Console.WriteLine("按 Ctrl+C 退出");
Console.WriteLine(new string('─', 60));

// ── 启动时先全量转译一次 ──────────────────────────────────────────────────────
foreach (var file in Directory.EnumerateFiles(workingDir, "*.cs"))
{
    TranspileFile(file);
}

// ── 文件监控 ──────────────────────────────────────────────────────────────────
using var watcher = new FileSystemWatcher(workingDir, "*.cs")
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
    // 过滤临时文件和备份文件
    if (ShouldIgnoreFile(e.FullPath))
        return;

    // FileSystemWatcher 可能连续触发多次，用简单延迟去抖
    Task.Delay(200).ContinueWith(_ => TranspileFile(e.FullPath));
}

void OnRenamed(object sender, RenamedEventArgs e)
{
    // 过滤临时文件
    if (ShouldIgnoreFile(e.FullPath))
        return;

    // 删除旧的 .py
    var oldPy = GetOutputPath(e.OldFullPath);
    if (File.Exists(oldPy))
    {
        File.Delete(oldPy);
        Console.WriteLine($"  删除: {Path.GetFileName(oldPy)}");
    }

    // 转译新文件
    Task.Delay(200).ContinueWith(_ => TranspileFile(e.FullPath));
}

void OnDeleted(object sender, FileSystemEventArgs e)
{
    // 过滤临时文件
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
    // 过滤临时文件
    if (ShouldIgnoreFile(csPath))
        return;

    // 检查文件是否存在
    if (!File.Exists(csPath))
    {
        Console.WriteLine($"[跳过] 文件不存在: {Path.GetFileName(csPath)}");
        return;
    }

    var fileName = Path.GetFileName(csPath);
    var pyPath = GetOutputPath(csPath);

    // Program.cs → main.py 是入口文件
    var nameNoExt = Path.GetFileNameWithoutExtension(csPath);
    var isEntry = nameNoExt.Equals("Program", StringComparison.OrdinalIgnoreCase);

    // 收集同目录下所有 .cs 文件名（不含扩展名），用于跨文件类引用
    var knownCsFiles = Directory.EnumerateFiles(workingDir, "*.cs")
        .Where(f => !ShouldIgnoreFile(f))  // 过滤临时文件
        .Select(f => Path.GetFileNameWithoutExtension(f))
        .ToHashSet(StringComparer.Ordinal);

    try
    {
        // 重试读取（文件可能还被编辑器占用）
        string source = ReadFileWithRetry(csPath);
        var result = CSharpToTfwrTranspiler.Transpile(
            source,
            isEntryFile: isEntry,
            knownCsFiles: knownCsFiles);
        File.WriteAllText(pyPath, result);

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Console.WriteLine($"[{timestamp}] {fileName} → {Path.GetFileName(pyPath)}{(isEntry ? " (入口)" : "")}");
    }
    catch (FileNotFoundException)
    {
        // 文件已被删除或移动，静默跳过
        Console.WriteLine($"[跳过] 文件已消失: {fileName}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[错误] {fileName}: {ex.Message}");
    }
}

string GetOutputPath(string csPath)
{
    var name = Path.GetFileNameWithoutExtension(csPath);
    var directory = Path.GetDirectoryName(csPath) ?? workingDir;

    // Program.cs → main.py (入口文件特殊映射)
    if (name.Equals("Program", StringComparison.OrdinalIgnoreCase))
        return Path.Combine(directory, "main.py");

    // 其他文件名全小写
    return Path.Combine(directory, name.ToLowerInvariant() + ".py");
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
    // 只处理 .cs 文件
    if (Path.GetExtension(path) != ".cs")
     return true;
    
    return false;
}
