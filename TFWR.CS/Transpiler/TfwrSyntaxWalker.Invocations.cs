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

        // new Xxx().Method() → module.method()（跨文件）或 method()（同文件）
        if (ma.Expression is ObjectCreationExpressionSyntax objCreate)
        {
            var typeName = objCreate.Type.ToString();
            // 检查是否为跨文件类
            if (IsKnownClassName(typeName) && !_localClassNames.Contains(typeName))
            {
                RegisterCrossFileImportIfNeeded(typeName);
                // 使用类所在的文件名作为模块名
                if (classToFileMap.TryGetValue(typeName, out var fileName))
                {
                    var moduleName = ToSnakeCase(fileName);
                    return $"{moduleName}.{ToSnakeCase(method)}({args})";
                }
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
            // 使用类所在的文件名作为模块名
            if (classToFileMap.TryGetValue(className, out var fileName))
            {
                var moduleName = ToSnakeCase(fileName);
                return $"{moduleName}.{ToSnakeCase(method)}({args})";
            }
            // 降级：如果找不到映射，使用类名
            return $"{ToSnakeCase(className)}.{ToSnakeCase(method)}({args})";
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

        // HashSet 方法
        if (IsHashSetMethod(method, ma, args, inv, out var setResult))
            return setResult;

        // Dictionary 方法
        if (IsDictionaryMethod(method, ma, args, inv, out var dictResult))
            return dictResult;

        // Random.Next() 等 → random()
        if (obj == "Random" || method == "Next" || method == "NextDouble")
            return $"random()";

        // 其他：保持 obj.method() 形式
        Console.WriteLine($"[警告] 未知的方法调用，可能无法正常运行: {obj}.{method}() at line {inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1}");
        return $"{TranspileExpression(ma.Expression)}.{ToSnakeCase(method)}({args})";
    }

    private bool IsListMethod(string method, MemberAccessExpressionSyntax ma, string args, InvocationExpressionSyntax inv, out string result)
    {
        result = string.Empty;
        var listExpr = TranspileExpression(ma.Expression);

        switch (method)
        {
            // ====== 支持的方法 ======

            case "Add":
                result = $"{listExpr}.append({args})";
                return true;

            case "Remove":
                result = $"{listExpr}.remove({args})";
                return true;

            case "Contains" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{args} in {listExpr}";
                return true;

            case "Insert":
                result = $"{listExpr}.insert({args})";
                return true;

            case "Clear" when inv.ArgumentList.Arguments.Count == 0:
                result = $"{listExpr} = []";
                return true;

            case "First" when inv.ArgumentList.Arguments.Count == 0:
                result = $"{listExpr}[0]";
                return true;

            case "Last" when inv.ArgumentList.Arguments.Count == 0:
                result = $"{listExpr}[-1]";
                return true;

            case "FirstOrDefault" when inv.ArgumentList.Arguments.Count == 0:
                // 不支持三元表达式，FirstOrDefault 需要用户手动实现
                WarnUnsupportedMethod(method, "List.FirstOrDefault (use if-else or try-except instead)", inv);
                result = $"# UNSUPPORTED: {listExpr}.FirstOrDefault() - use if len({listExpr}) > 0: x = {listExpr}[0]";
                return true;

            case "LastOrDefault" when inv.ArgumentList.Arguments.Count == 0:
                // 不支持三元表达式，LastOrDefault 需要用户手动实现
                WarnUnsupportedMethod(method, "List.LastOrDefault (use if-else or try-except instead)", inv);
                result = $"# UNSUPPORTED: {listExpr}.LastOrDefault() - use if len({listExpr}) > 0: x = {listExpr}[-1]";
                return true;

            case "Any" when inv.ArgumentList.Arguments.Count == 0:
                result = $"(len({listExpr}) > 0)";
                return true;

            case "ElementAt" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{listExpr}[{args}]";
                return true;

            case "ElementAtOrDefault" when inv.ArgumentList.Arguments.Count == 1:
                // 不支持三元表达式，ElementAtOrDefault 需要用户手动实现
                WarnUnsupportedMethod(method, "List.ElementAtOrDefault (use if-else or try-except instead)", inv);
                result = $"# UNSUPPORTED: {listExpr}.ElementAtOrDefault({args}) - use if {args} < len({listExpr}): x = {listExpr}[{args}]";
                return true;

            case "IndexOf" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{listExpr}.index({args})";
                return true;

            case "RemoveAt" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{listExpr}.pop({args})";
                return true;

            // ====== 不支持的方法（输出警告） ======

            case "Sort":
                WarnUnsupportedMethod(method, "List.Sort", inv);
                result = $"# UNSUPPORTED: {listExpr}.sort()";
                return true;

            case "Reverse":
                WarnUnsupportedMethod(method, "List.Reverse", inv);
                result = $"# UNSUPPORTED: {listExpr}.reverse()";
                return true;

            case "OrderBy":
            case "OrderByDescending":
            case "ThenBy":
            case "ThenByDescending":
                WarnUnsupportedMethod(method, "LINQ ordering", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ ordering";
                return true;

            case "Where":
            case "Select":
            case "SelectMany":
                WarnUnsupportedMethod(method, "LINQ query", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ query";
                return true;

            case "GroupBy":
            case "Join":
            case "GroupJoin":
                WarnUnsupportedMethod(method, "LINQ grouping/joining", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ grouping";
                return true;

            case "All":
            case "Count" when inv.ArgumentList.Arguments.Count > 0:
            case "Any" when inv.ArgumentList.Arguments.Count > 0:
                WarnUnsupportedMethod(method, "LINQ with predicate", inv);
                result = $"# UNSUPPORTED: {listExpr}.{ToSnakeCase(method)}() with predicate";
                return true;

            case "Skip":
            case "Take":
            case "SkipWhile":
            case "TakeWhile":
                WarnUnsupportedMethod(method, "LINQ pagination", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ pagination";
                return true;

            case "Distinct":
            case "Union":
            case "Intersect":
            case "Except":
                WarnUnsupportedMethod(method, "LINQ set operations", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ set operations";
                return true;

            case "Concat":
            case "Zip":
                WarnUnsupportedMethod(method, "LINQ combining", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ combining";
                return true;

            case "Aggregate":
            case "Sum":
            case "Average":
            case "Min":
            case "Max":
                WarnUnsupportedMethod(method, "LINQ aggregation", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ aggregation";
                return true;

            case "ToList":
            case "ToArray":
            case "ToDictionary":
            case "ToHashSet":
                WarnUnsupportedMethod(method, "LINQ conversion", inv);
                result = $"# UNSUPPORTED: {listExpr} LINQ conversion";
                return true;

            case "ForEach":
                WarnUnsupportedMethod(method, "List.ForEach", inv);
                result = $"# UNSUPPORTED: Use for loop instead of {listExpr}.ForEach()";
                return true;

            case "Find":
            case "FindAll":
            case "FindIndex":
            case "FindLast":
            case "FindLastIndex":
                WarnUnsupportedMethod(method, "List.Find methods", inv);
                result = $"# UNSUPPORTED: {listExpr}.{ToSnakeCase(method)}()";
                return true;

            case "Exists":
            case "TrueForAll":
                WarnUnsupportedMethod(method, "List predicate methods", inv);
                result = $"# UNSUPPORTED: {listExpr}.{ToSnakeCase(method)}()";
                return true;

            case "ConvertAll":
                WarnUnsupportedMethod(method, "List.ConvertAll", inv);
                result = $"# UNSUPPORTED: {listExpr}.convert_all()";
                return true;

            case "AddRange":
            case "InsertRange":
            case "RemoveRange":
            case "RemoveAll":
                WarnUnsupportedMethod(method, "List range methods", inv);
                result = $"# UNSUPPORTED: {listExpr}.{ToSnakeCase(method)}()";
                return true;

            case "BinarySearch":
            case "CopyTo":
            case "GetRange":
            case "AsReadOnly":
                WarnUnsupportedMethod(method, $"List.{method}", inv);
                result = $"# UNSUPPORTED: {listExpr}.{ToSnakeCase(method)}()";
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 输出不支持方法的警告信息
    /// </summary>
    private static void WarnUnsupportedMethod(string method, string description, InvocationExpressionSyntax inv)
    {
        var lineNumber = inv.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        Console.WriteLine($"[警告] 不支持的方法: {method} ({description}) at line {lineNumber}");
    }

    /// <summary>
    /// 处理 HashSet 方法调用
    /// </summary>
    private bool IsHashSetMethod(string method, MemberAccessExpressionSyntax ma, string args, InvocationExpressionSyntax inv, out string result)
    {
        result = string.Empty;
        var setExpr = TranspileExpression(ma.Expression);

        switch (method)
        {
            // ====== 支持的方法 ======

            case "Add" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{setExpr}.add({args})";
                return true;

            case "Remove" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{setExpr}.remove({args})";
                return true;

            case "Contains" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{args} in {setExpr}";
                return true;

            case "Clear" when inv.ArgumentList.Arguments.Count == 0:
                result = $"{setExpr}.clear()";
                return true;

            // ====== 不确定是否支持的方法 ======

            case "Count" when inv.ArgumentList.Arguments.Count == 0:
                // 不确定游戏是否支持 len(set)
                WarnUnsupportedMethod(method, "HashSet.Count (不确定游戏是否支持)", inv);
                result = $"len({setExpr})  # UNCERTAIN: 游戏可能不支持";
                return true;

            case "Any" when inv.ArgumentList.Arguments.Count == 0:
                // 不确定游戏是否支持
                WarnUnsupportedMethod(method, "HashSet.Any (不确定游戏是否支持)", inv);
                result = $"(len({setExpr}) > 0)  # UNCERTAIN: 游戏可能不支持";
                return true;

            // ====== 不支持的方法（输出警告） ======

            case "UnionWith":
            case "IntersectWith":
            case "ExceptWith":
            case "SymmetricExceptWith":
                WarnUnsupportedMethod(method, "HashSet set operations", inv);
                result = $"# UNSUPPORTED: {setExpr}.{ToSnakeCase(method)}()";
                return true;

            case "IsSubsetOf":
            case "IsSupersetOf":
            case "IsProperSubsetOf":
            case "IsProperSupersetOf":
            case "Overlaps":
            case "SetEquals":
                WarnUnsupportedMethod(method, "HashSet comparison methods", inv);
                result = $"# UNSUPPORTED: {setExpr}.{ToSnakeCase(method)}()";
                return true;

            case "CopyTo":
            case "TrimExcess":
            case "EnsureCapacity":
                WarnUnsupportedMethod(method, $"HashSet.{method}", inv);
                result = $"# UNSUPPORTED: {setExpr}.{ToSnakeCase(method)}()";
                return true;

            case "RemoveWhere":
                WarnUnsupportedMethod(method, "HashSet.RemoveWhere", inv);
                result = $"# UNSUPPORTED: {setExpr}.remove_where() - use for loop with condition";
                return true;

            // LINQ 方法
            case "Where":
            case "Select":
            case "OrderBy":
            case "OrderByDescending":
                WarnUnsupportedMethod(method, "LINQ on HashSet", inv);
                result = $"# UNSUPPORTED: {setExpr} LINQ query";
                return true;

            case "ToList":
            case "ToArray":
                WarnUnsupportedMethod(method, "HashSet.ToList/ToArray (可能不需要，集合可以直接遍历)", inv);
                result = $"# UNSUPPORTED: {setExpr}.{ToSnakeCase(method)}() - 集合可以直接遍历";
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// 处理 Dictionary 方法调用
    /// </summary>
    private bool IsDictionaryMethod(string method, MemberAccessExpressionSyntax ma, string args, InvocationExpressionSyntax inv, out string result)
    {
        result = string.Empty;
        var dictExpr = TranspileExpression(ma.Expression);

        switch (method)
        {
            // ====== 支持的方法 ======

            case "ContainsKey" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{args} in {dictExpr}";
                return true;

            case "ContainsValue" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{args} in {dictExpr}.values()";
                return true;

            case "TryGetValue" when inv.ArgumentList.Arguments.Count == 2:
                // dict.TryGetValue(key, out value) → Python 中需要转换
                WarnUnsupportedMethod(method, "Dictionary.TryGetValue (use dict.get() instead)", inv);
                result = $"# UNSUPPORTED: Use {dictExpr}.get({args.Split(',')[0].Trim()}, None) instead";
                return true;

            case "Add" when inv.ArgumentList.Arguments.Count == 2:
                var keyValue = args.Split([", "], StringSplitOptions.None);
                if (keyValue.Length == 2)
                {
                    result = $"{dictExpr}[{keyValue[0]}] = {keyValue[1]}";
                    return true;
                }
                return false;

            case "Remove" when inv.ArgumentList.Arguments.Count == 1:
                result = $"{dictExpr}.pop({args})";
                return true;

            case "Clear" when inv.ArgumentList.Arguments.Count == 0:
                result = $"{dictExpr} = {{}}";
                return true;

            case "Keys" when inv.ArgumentList.Arguments.Count == 0:
                WarnUnsupportedMethod(method, "Dictionary.Keys", inv);
                result = $"# UNSUPPORTED: list({dictExpr}.keys())";
                return true;

            case "Values" when inv.ArgumentList.Arguments.Count == 0:
                WarnUnsupportedMethod(method, "Dictionary.Values", inv);
                result = $"# UNSUPPORTED: list({dictExpr}.values())";
                return true;

            case "TryAdd":
                WarnUnsupportedMethod(method, "Dictionary.TryAdd", inv);
                result = $"# UNSUPPORTED: {dictExpr}.try_add()";
                return true;

            case "GetValueOrDefault":
                WarnUnsupportedMethod(method, "Dictionary.GetValueOrDefault (use dict.get() instead)", inv);
                result = $"# UNSUPPORTED: Use {dictExpr}.get({args}, None) instead";
                return true;

            case "EnsureCapacity":
            case "TrimExcess":
                WarnUnsupportedMethod(method, $"Dictionary.{method}", inv);
                result = $"# UNSUPPORTED: {dictExpr}.{ToSnakeCase(method)}()";
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

                // 使用类所在的文件名作为模块名
                if (classToFileMap.TryGetValue(typeName, out var fileName))
                {
                    var moduleName = ToSnakeCase(fileName);
                    return $"{moduleName}.{ToSnakeCase(member)}";
                }
                // 降级：如果找不到映射，使用类名
                return $"{ToSnakeCase(typeName)}.{ToSnakeCase(member)}";
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
