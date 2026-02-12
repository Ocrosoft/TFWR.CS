using Microsoft.CodeAnalysis.CSharp;

namespace TFWR.CS.Transpiler;

/// <summary>
/// 将 C# 代码（使用 TFWR.CS.REF 定义）转译为 The Farmer Was Replaced 的类 Python 脚本。
/// </summary>
public class CSharpToTfwrTranspiler
{
    /// <summary>
    /// 将一段 C# 源码转译为 TFWR 游戏脚本。
    /// </summary>
    /// <param name="csharpSource">C# 源码。</param>
    /// <param name="isEntryFile">是否为入口文件（main.cs / Program.cs）。
    /// 若为 true，当检测到 Program.Main 方法时会在末尾自动追加调用。</param>
    /// <param name="classToFileMap">类名到文件名（不含扩展名）的映射，用于跨文件引用。</param>
    /// <param name="currentFileName">当前文件名（不含扩展名），用于判断是否需要 import。</param>
    public static string Transpile(
        string csharpSource,
        bool isEntryFile = false,
        IReadOnlyDictionary<string, string>? classToFileMap = null,
        string? currentFileName = null)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpSource);
        var root = tree.GetCompilationUnitRoot();
        var builder = new PythonCodeBuilder();
        var walker = new TfwrSyntaxWalker(
            builder,
            isEntryFile,
            classToFileMap ?? new Dictionary<string, string>(),
            currentFileName ?? "");
        walker.Visit(root);
        walker.EmitEntryPointCallIfNeeded();
        return builder.ToString();
    }
}
