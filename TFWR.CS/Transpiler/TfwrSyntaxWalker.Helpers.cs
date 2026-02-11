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
    private string BuildDeconstructionPattern(ParenthesizedVariableDesignationSyntax paren)
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
    /// 从比较器表达式中提取 key 函数。
    /// 例如：a.Item3.CompareTo(b.Item3) → 提取 Item3（去掉参数前缀 a.）
    /// </summary>
    private string ExtractKeyFromComparer(ExpressionSyntax expr, string paramName)
    {
        var exprStr = expr.ToString();

        // 去掉参数前缀：a.Field → Field
        if (exprStr.StartsWith($"{paramName}."))
        {
            var keyPart = exprStr.Substring(paramName.Length + 1);

            // 转译 key 部分（可能包含多层访问：a.Item3 → item3）
            // 需要创建一个临时的表达式节点来转译
            // 简化处理：直接转为 snake_case
            return ToSnakeCase(keyPart.Replace(".", "_"));
        }

        return ToSnakeCase(exprStr);
    }

    /// <summary>
    /// 判断标识符是否为已知的类名（当前文件定义的 或 同目录下其他文件中的）。
    /// </summary>
    private bool IsKnownClassName(string name)
    {
        // 当前文件定义的类
        if (_localClassNames.Contains(name)) return true;

        // 同目录下的 .cs 文件名与类名匹配（约定：文件名 = 类名）
        return knownCsFiles.Contains(name);
    }

    /// <summary>
    /// 如果类不是当前文件定义的，记录需要自动生成 import。
    /// 模块名基于文件名（knownCsFiles），而非类名。
    /// </summary>
    private void RegisterCrossFileImportIfNeeded(string className)
    {
        // 如果在当前文件定义，不需要 import
        if (_localClassNames.Contains(className))
            return;

        // 只有当类名匹配已知的 .cs 文件名时才生成 import
        if (!knownCsFiles.Contains(className))
            return;

        _crossFileImports.Add(className);
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
