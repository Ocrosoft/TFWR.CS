using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TFWR.CS.Transpiler;

/// <summary>
/// TfwrSyntaxWalker 的方法调用和成员访问处理部分
/// </summary>
internal partial class TfwrSyntaxWalker
{
    private string TranspileInvocation(InvocationExpressionSyntax inv)
    {
        var args = string.Join(", ", inv.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression)));

        switch (inv.Expression)
        {
            case MemberAccessExpressionSyntax ma:
                return TranspileMemberAccessInvocation(ma, args, inv);

            case IdentifierNameSyntax id:
                {
                    var method = id.Identifier.Text;
                    if (NameMappings.TryGetBuiltinMethod(method, out var func))
                        return $"{func}({args})";

                    return $"{ToSnakeCase(method)}({args})";
                }

            default:
                return $"{TranspileExpression(inv.Expression)}({args})";
        }
    }

    private string TranspileMemberAccessInvocation(MemberAccessExpressionSyntax ma, string args, InvocationExpressionSyntax inv)
    {
        var method = ma.Name.Identifier.Text;

        // Console.WriteLine / Console.Write → quick_print（优先检查）
        if (ma.Expression is IdentifierNameSyntax { Identifier.Text: "Console" })
        {
            if (method is "WriteLine" or "Write")
                return $"quick_print({args})";
        }

        var obj = ma.Expression.ToString();

        // Tuple.Create(...) → (arg1, arg2, ...)
        if (obj == "Tuple" && method == "Create")
        {
            // 如果只有一个参数，需要添加逗号: (x,)
            if (inv.ArgumentList.Arguments.Count == 1)
                return $"({args},)";
            return $"({args})";
        }

        // TFWR.XXX → 游戏内置
        if (obj == "TFWR" && NameMappings.TryGetBuiltinMethod(method, out var func))
            return $"{func}({args})";

        // new Xxx().Method() → module.method()（跨文件）或 method()（同文件）
        if (ma.Expression is ObjectCreationExpressionSyntax objCreate)
        {
            var typeName = objCreate.Type.ToString();
            // 检查是否为跨文件类
            if (IsKnownClassName(typeName) && !_localClassNames.Contains(typeName))
            {
                RegisterCrossFileImportIfNeeded(typeName);
                var moduleName = ToSnakeCase(typeName);
                return $"{moduleName}.{ToSnakeCase(method)}({args})";
            }
            return $"{ToSnakeCase(method)}({args})";
        }

        // this.Method() → method()
        if (ma.Expression is ThisExpressionSyntax)
            return $"{ToSnakeCase(method)}({args})";

        // ClassName.Method() → module.method()（跨文件）或 method()（同文件）
        if (ma.Expression is IdentifierNameSyntax classId && IsKnownClassName(classId.Identifier.Text))
        {
            var className = classId.Identifier.Text;
            // 同文件的类 → 直接调用 method()
            if (_localClassNames.Contains(className))
                return $"{ToSnakeCase(method)}({args})";

            // 跨文件的类 → module.method()
            RegisterCrossFileImportIfNeeded(className);
            var moduleName = ToSnakeCase(className);
            return $"{moduleName}.{ToSnakeCase(method)}({args})";
        }

        // Math.Min / Math.Max / Math.Abs
        var dotnetKey = $"{obj}.{method}";
        if (NameMappings.TryGetDotNetBuiltin(dotnetKey, out var builtinFunc))
            return $"{builtinFunc}({args})";

        // obj.ToString() → str(obj)
        if (method == "ToString")
            return $"str({TranspileExpression(ma.Expression)})";

        // obj.Length / obj.Count → len(obj)
        if (method is "Length" or "Count")
            return $"len({TranspileExpression(ma.Expression)})";

        // List 方法
        if (IsListMethod(method, ma, args, inv, out var listResult))
            return listResult;

        // dict.ContainsKey(k) → k in dict
        if (method == "ContainsKey" && inv.ArgumentList.Arguments.Count == 1)
            return $"{args} in {TranspileExpression(ma.Expression)}";

        // Random.Next() 等 → random()
        if (obj == "Random" || method == "Next" || method == "NextDouble")
            return $"random()";

        // 其他：保持 obj.method() 形式
        return $"{TranspileExpression(ma.Expression)}.{ToSnakeCase(method)}({args})";
    }

    private bool IsListMethod(string method, MemberAccessExpressionSyntax ma, string args, InvocationExpressionSyntax inv, out string result)
    {
        result = string.Empty;

        switch (method)
        {
            case "Add":
                result = $"{TranspileExpression(ma.Expression)}.append({args})";
                return true;

            case "Remove":
                result = $"{TranspileExpression(ma.Expression)}.remove({args})";
                return true;

            case "Contains" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{args} in {TranspileExpression(ma.Expression)}";
                return true;

            case "Insert":
                result = $"{TranspileExpression(ma.Expression)}.insert({args})";
                return true;

            case "Clear" when inv.ArgumentList.Arguments.Count == 0:
                result = $"{TranspileExpression(ma.Expression)} = []";
                return true;

            default:
                return false;
        }
    }

    private string TranspileMemberAccess(MemberAccessExpressionSyntax ma)
    {
        var obj = ma.Expression;
        var member = ma.Name.Identifier.Text;

        // this.x → x
        if (obj is ThisExpressionSyntax)
            return ToSnakeCase(member);

        // Tuple.ItemN → tuple[N-1]（TFWR 不支持 .Item1 这样访问）
        if (member.StartsWith("Item") && int.TryParse(member.Substring(4), out var itemNum))
        {
            var index = itemNum - 1; // Item1 → [0], Item2 → [1], etc.
            return $"{TranspileExpression(obj)}[{index}]";
        }

        // 枚举和已知类型
        if (obj is IdentifierNameSyntax typeId)
        {
            var typeName = typeId.Identifier.Text;

            // Direction.North → North
            if (typeName == "Direction" && NameMappings.IsDirectionValue(member))
                return NameMappings.NormalizeDirection(member);

            // Entity.Bush → Entities.Bush
            if (NameMappings.TryGetEnumPrefix(typeName, out var prefix))
                return $"{prefix}.{member}";

            // ClassName.StaticField → module.static_field（跨文件）或 static_field（同文件）
            if (IsKnownClassName(typeName))
            {
                // 同文件的类 → 直接访问
                if (_localClassNames.Contains(typeName))
                    return ToSnakeCase(member);

                // 跨文件的类 → module.field
                RegisterCrossFileImportIfNeeded(typeName);
                var moduleName = ToSnakeCase(typeName);
                return $"{moduleName}.{ToSnakeCase(member)}";
            }
        }

        // obj.Length / obj.Count → len(obj)
        if (member is "Length" or "Count")
            return $"len({TranspileExpression(obj)})";

        // 可空类型的 HasValue 属性
        if (member == "HasValue")
            return $"({TranspileExpression(obj)} != None)";

        // 可空类型的 Value 属性 → 直接返回对象本身（Python 不需要 .Value）
        if (member == "Value")
            return TranspileExpression(obj);

        return $"{TranspileExpression(obj)}.{ToSnakeCase(member)}";
    }
}
