using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TFWR.CS.Transpiler;

/// <summary>
/// Roslyn SyntaxWalker，遍历 C# AST 并输出 TFWR 脚本。
/// </summary>
internal partial class TfwrSyntaxWalker(
    PythonCodeBuilder builder,
    bool isEntryFile,
    IReadOnlyDictionary<string, string> classToFileMap,
    string currentFileName) : CSharpSyntaxWalker
{
    private readonly HashSet<string> _globalVarsWritten = [];
    private string? _entryPointFuncName;

    // 当前类的所有字段名（用于识别方法中需要 global 声明的变量）
    private readonly HashSet<string> _classFieldNames = [];

    // 当前文件中定义的所有类名
    private readonly HashSet<string> _localClassNames = [];

    // 跨文件引用的类名 → 需要自动生成 import（存储文件名而非类名）
    private readonly HashSet<string> _crossFileImports = [];

    // ========================================================================
    // Top-level statements (CompilationUnit) — 直接遍历子节点
    // ========================================================================
    public override void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        // ── 预扫描：收集当前文件中定义的所有类名 ──
        foreach (var cls in node.DescendantNodes().OfType<ClassDeclarationSyntax>())
            _localClassNames.Add(cls.Identifier.Text);

        // 步骤2：成员遍历（类 / 方法 / 命名空间等）
        foreach (var member in node.Members)
            Visit(member);

        // ── 在文件头部插入跨文件引用的 import（自动检测）──
        foreach (var fileName in _crossFileImports.Order())
        {
            var moduleName = ToSnakeCase(fileName);
            builder.InsertImportAfterExisting($"import {moduleName}");
        }
    }

    // ========================================================================
    // GlobalStatement 及 全局语句
    // ========================================================================
    public override void VisitGlobalStatement(GlobalStatementSyntax node)
    {
        VisitStatement(node.Statement);
    }

    // ========================================================================
    // 命名空间 / 类 — 展开内容
    // ========================================================================
    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        foreach (var member in node.Members)
        {
            WarnIfIgnoredTopLevelType(member);
            Visit(member);
        }
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        foreach (var member in node.Members)
        {
            WarnIfIgnoredTopLevelType(member);
            Visit(member);
        }
    }

    private static void WarnIfIgnoredTopLevelType(MemberDeclarationSyntax member)
    {
        switch (member)
        {
            case EnumDeclarationSyntax enumDecl:
                {
                    var lineNumber = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine($"[警告] 枚举声明被忽略: {enumDecl.Identifier.Text} at line {lineNumber}");
                    break;
                }
            case InterfaceDeclarationSyntax iface:
                {
                    var lineNumber = iface.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine($"[警告] 接口声明被忽略: {iface.Identifier.Text} at line {lineNumber}");
                    break;
                }
            case StructDeclarationSyntax structDecl:
                {
                    var lineNumber = structDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine($"[警告] 结构体声明被忽略（将当作类处理）: {structDecl.Identifier.Text} at line {lineNumber}");
                    break;
                }
            case RecordDeclarationSyntax recordDecl:
                {
                    var lineNumber = recordDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine($"[警告] 记录类型声明被忽略: {recordDecl.Identifier.Text} at line {lineNumber}");
                    break;
                }
            case DelegateDeclarationSyntax del:
                {
                    var lineNumber = del.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine($"[警告] 委托声明被忽略: {del.Identifier.Text} at line {lineNumber}");
                    break;
                }
        }
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        // 收集类的字段名（用于后续方法中的 global 声明）
        foreach (var member in node.Members)
        {
            if (member is FieldDeclarationSyntax field)
            {
                foreach (var v in field.Declaration.Variables)
                {
                    _classFieldNames.Add(v.Identifier.Text);
                    _globalVarsWritten.Add(v.Identifier.Text);
                }
            }
        }

        // 先输出字段和常量（作为模块级变量）
        foreach (var member in node.Members)
        {
            if (member is FieldDeclarationSyntax field)
                VisitFieldDeclaration(field);
        }

        // 输出构造函数体（作为顶层代码）
        foreach (var member in node.Members)
        {
            if (member is ConstructorDeclarationSyntax ctor)
                VisitConstructorDeclaration(ctor);
        }

        // 输出方法
        foreach (var member in node.Members)
        {
            if (member is MethodDeclarationSyntax method)
                Visit(method);
            else if (member is ClassDeclarationSyntax nested)
                Visit(nested);
        }

        // 对被忽略的类成员输出警告
        foreach (var member in node.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax:
                case MethodDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case ClassDeclarationSyntax:
                    // 已处理
                    break;
                case PropertyDeclarationSyntax prop:
                    var lineNumber = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Console.WriteLine($"[警告] 属性声明被忽略: {prop.Identifier.Text} at line {lineNumber}（仅支持字段，不支持 get/set 逻辑）");
                    break;
                case EventDeclarationSyntax evt:
                    WarnIgnoredMember(evt, $"事件声明: {evt.Identifier.Text}");
                    break;
                case EventFieldDeclarationSyntax evtField:
                    WarnIgnoredMember(evtField, "事件字段声明");
                    break;
                case IndexerDeclarationSyntax indexer:
                    WarnIgnoredMember(indexer, "索引器声明");
                    break;
                case OperatorDeclarationSyntax op:
                    WarnIgnoredMember(op, $"运算符重载: {op.OperatorToken.Text}");
                    break;
                case ConversionOperatorDeclarationSyntax conv:
                    WarnIgnoredMember(conv, "类型转换运算符");
                    break;
                case DelegateDeclarationSyntax del:
                    WarnIgnoredMember(del, $"委托声明: {del.Identifier.Text}");
                    break;
                case DestructorDeclarationSyntax:
                    WarnIgnoredMember(member, "析构函数");
                    break;
                case EnumDeclarationSyntax enumDecl:
                    WarnIgnoredMember(enumDecl, $"枚举声明: {enumDecl.Identifier.Text}");
                    break;
                case InterfaceDeclarationSyntax iface:
                    WarnIgnoredMember(iface, $"接口声明: {iface.Identifier.Text}");
                    break;
                case StructDeclarationSyntax structDecl:
                    WarnIgnoredMember(structDecl, $"结构体声明: {structDecl.Identifier.Text}");
                    break;
                case RecordDeclarationSyntax recordDecl:
                    WarnIgnoredMember(recordDecl, $"记录类型声明: {recordDecl.Identifier.Text}");
                    break;
                    // 其他未知成员不需要额外警告
            }
        }
    }

    // ========================================================================
    // 字段声明 → 模块级全局变量
    // ========================================================================
    private new void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        foreach (var v in node.Declaration.Variables)
        {
            var name = ToSnakeCase(v.Identifier.Text);

            if (v.Initializer != null)
            {
                var value = TranspileExpression(v.Initializer.Value);
                builder.AppendLine($"{name} = {value}");
            }
            else
            {
                // 对于没有初始值的字段，给默认值
                var typeName = node.Declaration.Type.ToString();
                var defaultVal = typeName switch
                {
                    "int" or "long" or "short" or "byte" => "0",
                    "float" or "double" or "decimal" => "0.0",
                    "bool" => "False",
                    "string" => "\"\"",
                    _ => "None"
                };
                builder.AppendLine($"{name} = {defaultVal}");
            }
        }
    }

    // ========================================================================
    // 构造函数 → 内联为顶层代码
    // ========================================================================
    private new void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (node.Body != null && node.Body.Statements.Count > 0)
        {
            builder.AppendBlankLine();
            VisitBlockBody(node.Body);
        }
    }

    // ========================================================================
    // 方法声明 → def
    // ========================================================================
    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var name = ToSnakeCase(node.Identifier.Text);
        var parameters = string.Join(", ", node.ParameterList.Parameters.Select(TranspileParameter));

        // 检测入口点: Program 类中的 Main 方法（static void Main / static int Main / static async Task Main）
        if (isEntryFile && IsEntryPointMethod(node))
        {
            _entryPointFuncName = name;
            // 不支持参数
            parameters = "";
        }

        builder.AppendBlankLine();
        builder.AppendLine($"def {name}({parameters}):");
        builder.IncreaseIndent();

        // 检查方法体内是否有对 global 变量的写入
        // 收集所有 global 声明
        EmitGlobalDeclarations(node);

        if (node.Body != null)
        {
            VisitBlockBody(node.Body);
        }
        else if (node.ExpressionBody != null)
        {
            var expr = node.ExpressionBody.Expression;
            var isVoidReturn = node.ReturnType.ToString() == "void";

            // 检查是否是递增/递减表达式
            if (expr is PostfixUnaryExpressionSyntax postfix && postfix.OperatorToken.Text is "++" or "--")
            {
                var operand = TranspileExpression(postfix.Operand);
                var op = postfix.OperatorToken.Text == "++" ? "+=" : "-=";

                if (isVoidReturn)
                {
                    // void 方法：只执行副作用
                    builder.AppendLine($"{operand} {op} 1");
                }
                else
                {
                    // 非 void 方法：先保存原值，再递增，最后返回原值
                    builder.AppendLine($"_temp = {operand}");
                    builder.AppendLine($"{operand} {op} 1");
                    builder.AppendLine("return _temp");
                }
            }
            else if (expr is PrefixUnaryExpressionSyntax prefix && prefix.OperatorToken.Text is "++" or "--")
            {
                var operand = TranspileExpression(prefix.Operand);
                var op = prefix.OperatorToken.Text == "++" ? "+=" : "-=";

                if (isVoidReturn)
                {
                    // void 方法：只执行副作用
                    builder.AppendLine($"{operand} {op} 1");
                }
                else
                {
                    // 非 void 方法：先递增，再返回新值
                    builder.AppendLine($"{operand} {op} 1");
                    builder.AppendLine($"return {operand}");
                }
            }
            else if (isVoidReturn && IsStatementExpression(expr))
            {
                // void 方法的其他副作用表达式（赋值、方法调用等）
                builder.AppendLine(TranspileExpression(expr));
            }
            else
            {
                // 其他情况：正常返回表达式值
                builder.AppendLine($"return {TranspileExpression(expr)}");
            }
        }
        else
        {
            builder.AppendLine("pass");
        }

        builder.DecreaseIndent();
        builder.AppendBlankLine();
    }

    // ========================================================================
    // 局部函数 → def
    // ========================================================================
    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var name = ToSnakeCase(node.Identifier.Text);
        var parameters = string.Join(", ", node.ParameterList.Parameters.Select(TranspileParameter));

        builder.AppendBlankLine();
        builder.AppendLine($"def {name}({parameters}):");
        builder.IncreaseIndent();

        EmitGlobalDeclarations(node);

        if (node.Body != null)
        {
            VisitBlockBody(node.Body);
        }
        else if (node.ExpressionBody != null)
        {
            builder.AppendLine($"return {TranspileExpression(node.ExpressionBody.Expression)}");
        }
        else
        {
            builder.AppendLine("pass");
        }

        builder.DecreaseIndent();
        builder.AppendBlankLine();
    }

    private string TranspileParameter(ParameterSyntax p)
    {
        var name = ToSnakeCase(p.Identifier.Text);
        if (p.Default != null)
            return $"{name} = {TranspileExpression(p.Default.Value)}";
        return name;
    }

    private void EmitGlobalDeclarations(SyntaxNode functionNode)
    {
        // 在 TFWR 中，如果函数体内对模块级变量赋值或修改，需要 global 声明
        var globalNames = new HashSet<string>();

        // 1. 检查赋值表达式：x = ... 或 this.x = ...
        var assignments = functionNode.DescendantNodes().OfType<AssignmentExpressionSyntax>();
        foreach (var a in assignments)
        {
            var varName = a.Left switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var n } => n.Identifier.Text,
                _ => null
            };
            if (varName != null && (_globalVarsWritten.Contains(varName) || _classFieldNames.Contains(varName)))
            {
                globalNames.Add(ToSnakeCase(varName));
            }
        }

        // 2. 检查 ++/-- 形式 (prefix/postfix unary)，包括 this.x++ 形式
        foreach (var unary in functionNode.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
        {
            var varName = unary.Operand switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var n } => n.Identifier.Text,
                _ => null
            };
            if (varName != null && (_globalVarsWritten.Contains(varName) || _classFieldNames.Contains(varName)))
                globalNames.Add(ToSnakeCase(varName));
        }
        foreach (var unary in functionNode.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            var varName = unary.Operand switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var n } => n.Identifier.Text,
                _ => null
            };
            if (varName != null && (_globalVarsWritten.Contains(varName) || _classFieldNames.Contains(varName)))
                globalNames.Add(ToSnakeCase(varName));
        }

        // 3. 检查 list 等修改方法调用：Add, Remove, Insert, Clear
        // 这些方法会修改列表，可能需要 global 声明
        var invocations = functionNode.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var inv in invocations)
        {
            if (inv.Expression is MemberAccessExpressionSyntax ma)
            {
                var method = ma.Name.Identifier.Text;

                // 判断是否是修改性方法
                if (method is "Add" or "Remove" or "Insert" or "Clear" or "RemoveAt")
                {
                    // 获取方法调用的对象（list 变量名）
                    var varName = ma.Expression switch
                    {
                        IdentifierNameSyntax id => id.Identifier.Text,
                        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var n } => n.Identifier.Text,
                        _ => null
                    };

                    if (varName != null && (_globalVarsWritten.Contains(varName) || _classFieldNames.Contains(varName)))
                    {
                        globalNames.Add(ToSnakeCase(varName));
                    }
                }
            }
        }

        // 输出 global 声明
        foreach (var g in globalNames.Order())
            builder.AppendLine($"global {g}");
    }

    public void EmitEntryPointCallIfNeeded()
    {
        if (_entryPointFuncName == null) return;
        builder.AppendBlankLine();
        builder.AppendLine($"{_entryPointFuncName}()");
    }

    private static bool IsEntryPointMethod(MethodDeclarationSyntax node)
    {
        if (node.Identifier.Text != "Main") return false;
        if (!node.Modifiers.Any(SyntaxKind.StaticKeyword)) return false;
        if (node.Parent is not ClassDeclarationSyntax) return false;
        return true;
    }

    /// <summary>
    /// 判断表达式是否是语句表达式（有副作用但不需要返回值）
    /// </summary>
    private static bool IsStatementExpression(ExpressionSyntax expr)
    {
        return expr is PostfixUnaryExpressionSyntax { OperatorToken.Text: "++" or "--" }
                    or PrefixUnaryExpressionSyntax { OperatorToken.Text: "++" or "--" }
                    or AssignmentExpressionSyntax
                    or InvocationExpressionSyntax;
    }

    private static void WarnIgnoredMember(MemberDeclarationSyntax member, string description)
    {
        var lineNumber = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        Console.WriteLine($"[警告] 已忽略的类成员: {description} at line {lineNumber}");
    }
}
