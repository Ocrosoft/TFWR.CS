using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TFWR.CS.Transpiler;

/// <summary>
/// TfwrSyntaxWalker 的辅助方法部分
/// </summary>
internal partial class TfwrSyntaxWalker
{
    /// <summary>
    /// Build deconstruction pattern for nested tuples
    /// Example: (companionType, (x, y)) → companion_type, (x, y)
    /// </summary>
    private static string BuildDeconstructionPattern(ParenthesizedVariableDesignationSyntax paren)
    {
        var parts = new List<string>();

        foreach (var variable in paren.Variables)
        {
            if (variable is SingleVariableDesignationSyntax single)
            {
                parts.Add(ToSnakeCase(single.Identifier.Text));
            }
            else if (variable is ParenthesizedVariableDesignationSyntax nested)
            {
                parts.Add($"({BuildDeconstructionPattern(nested)})");
            }
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// 判断标识符是否为已知的类名（当前文件定义的 或 同目录下其他文件中的）。
    /// </summary>
    private bool IsKnownClassName(string name)
    {
        // 当前文件定义的类
        if (_localClassNames.Contains(name)) return true;

        // 在类到文件映射中查找
        return classToFileMap.ContainsKey(name);
    }

    /// <summary>
    /// 如果类不是当前文件定义的，记录需要自动生成 import。
    /// 存储对应的文件名（而非类名），用于生成 import 语句。
    /// </summary>
    private void RegisterCrossFileImportIfNeeded(string className)
    {
        // 如果在当前文件定义，不需要 import
        if (_localClassNames.Contains(className))
            return;

        // 查找类所在的文件
        if (!classToFileMap.TryGetValue(className, out var fileName))
            return;

        // 如果类在当前文件中，不需要 import
        if (fileName.Equals(currentFileName, StringComparison.OrdinalIgnoreCase))
            return;

        // 记录需要 import 的文件名
        _crossFileImports.Add(fileName);
    }

    // ========================================================================
    // PascalCase / camelCase → snake_case
    // ========================================================================
    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        if (name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c)))
            return name.ToLowerInvariant();

        if (name.Contains('_'))
            return name;

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
}
