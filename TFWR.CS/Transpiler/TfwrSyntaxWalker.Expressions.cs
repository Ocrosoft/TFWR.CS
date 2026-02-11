using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TFWR.CS.Transpiler;

/// <summary>
/// TfwrSyntaxWalker 的表达式处理部分
/// </summary>
internal partial class TfwrSyntaxWalker
{
    // ========================================================================
    // 表达式转译
    // ========================================================================
    private string TranspileExpression(ExpressionSyntax expr)
    {
        return expr switch
        {
            LiteralExpressionSyntax lit => TranspileLiteral(lit),
            IdentifierNameSyntax id => TranspileIdentifier(id),
            InvocationExpressionSyntax inv => TranspileInvocation(inv),
            MemberAccessExpressionSyntax ma => TranspileMemberAccess(ma),
            BinaryExpressionSyntax bin => TranspileBinary(bin),
            PrefixUnaryExpressionSyntax pre => TranspilePrefixUnary(pre),
            PostfixUnaryExpressionSyntax post => TranspilePostfixUnary(post),
            ParenthesizedExpressionSyntax paren => $"({TranspileExpression(paren.Expression)})",
            AssignmentExpressionSyntax assign => TranspileAssignment(assign),
            ConditionalExpressionSyntax cond => TranspileConditional(cond),
            CastExpressionSyntax cast => TranspileCast(cast),
            ObjectCreationExpressionSyntax objCreate => TranspileObjectCreation(objCreate),
            ImplicitObjectCreationExpressionSyntax => "{}",
            ArrayCreationExpressionSyntax arrCreate => TranspileArrayCreation(arrCreate),
            ImplicitArrayCreationExpressionSyntax implArr => TranspileImplicitArrayCreation(implArr),
            InitializerExpressionSyntax init => TranspileInitializer(init),
            CollectionExpressionSyntax coll => TranspileCollectionExpression(coll),
            InterpolatedStringExpressionSyntax interp => TranspileInterpolatedString(interp),
            ElementAccessExpressionSyntax elemAccess => TranspileElementAccess(elemAccess),
            TupleExpressionSyntax tuple => TranspileTuple(tuple),
            DeclarationExpressionSyntax declExpr => TranspileDeclarationExpression(declExpr),
            DefaultExpressionSyntax => "None",
            ThisExpressionSyntax => "self",
            TypeOfExpressionSyntax typeOf => $"type({TranspileTypeName(typeOf.Type)})",
            IsPatternExpressionSyntax isPat => TranspileIsPattern(isPat),
            LambdaExpressionSyntax lambda => HandleUnsupportedExpression(lambda, "Lambda 表达式"),
            _ => HandleUnsupportedExpression(expr, "未知表达式")
        };
    }

    private string HandleUnsupportedExpression(ExpressionSyntax expr, string description)
    {
        var lineNumber = expr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var message = $"无法转译的{description}: {expr.Kind()} at line {lineNumber}";
        Console.WriteLine($"[警告] {message}");

        // 对于未知表达式，直接返回原始文本
        return $"/* {expr} */";
    }

    private static string TranspileLiteral(LiteralExpressionSyntax lit)
    {
        return lit.Kind() switch
        {
            SyntaxKind.TrueLiteralExpression => "True",
            SyntaxKind.FalseLiteralExpression => "False",
            SyntaxKind.NullLiteralExpression => "None",
            SyntaxKind.StringLiteralExpression => $"\"{lit.Token.ValueText}\"",
            SyntaxKind.CharacterLiteralExpression => $"\"{lit.Token.ValueText}\"",
            SyntaxKind.NumericLiteralExpression => lit.Token.Text.TrimEnd('f', 'F', 'd', 'D', 'm', 'M', 'L', 'l', 'u', 'U'),
            SyntaxKind.DefaultLiteralExpression => "None",
            _ => lit.Token.Text
        };
    }

    private static string TranspileIdentifier(IdentifierNameSyntax id)
    {
        var name = id.Identifier.Text;
        // Direction 常量直接使用（并规范化别名）
        if (NameMappings.IsDirectionValue(name))
            return NameMappings.NormalizeDirection(name);
        // var → 忽略类型名
        if (name == "var") return name;
        return ToSnakeCase(name);
    }

    private string TranspileBinary(BinaryExpressionSyntax bin)
    {
        var left = TranspileExpression(bin.Left);
        var right = TranspileExpression(bin.Right);

        var op = bin.Kind() switch
        {
            SyntaxKind.AddExpression => "+",
            SyntaxKind.SubtractExpression => "-",
            SyntaxKind.MultiplyExpression => "*",
            SyntaxKind.DivideExpression => GetDivisionOperator(bin),
            SyntaxKind.ModuloExpression => "%",
            SyntaxKind.EqualsExpression => "==",
            SyntaxKind.NotEqualsExpression => "!=",
            SyntaxKind.LessThanExpression => "<",
            SyntaxKind.LessThanOrEqualExpression => "<=",
            SyntaxKind.GreaterThanExpression => ">",
            SyntaxKind.GreaterThanOrEqualExpression => ">=",
            SyntaxKind.LogicalAndExpression => "and",
            SyntaxKind.LogicalOrExpression => "or",
            SyntaxKind.BitwiseAndExpression => "&",
            SyntaxKind.BitwiseOrExpression => "|",
            SyntaxKind.ExclusiveOrExpression => "^",
            SyntaxKind.LeftShiftExpression => "<<",
            SyntaxKind.RightShiftExpression => ">>",
            SyntaxKind.IsExpression => "is",
            _ => bin.OperatorToken.Text
        };

        // Null coalescing: a ?? b
        // TFWR 不支持三元表达式，生成 (a if a is not None else b) 并添加警告
        if (bin.IsKind(SyntaxKind.CoalesceExpression))
            return $"({left} if {left} is not None else {right})  # WARNING: TFWR may not support ternary";

        return $"{left} {op} {right}";
    }

    private static string GetDivisionOperator(BinaryExpressionSyntax bin)
    {
        if (bin.Left is LiteralExpressionSyntax { Token.Value: int or long } &&
            bin.Right is LiteralExpressionSyntax { Token.Value: int or long })
            return "//";
        return "/";
    }

    private string TranspilePrefixUnary(PrefixUnaryExpressionSyntax pre)
    {
        var operand = TranspileExpression(pre.Operand);
        return pre.OperatorToken.Text switch
        {
            "!" => $"not {operand}",
            "-" => $"-{operand}",
            "~" => $"~{operand}",
            // ++x 和 --x：副作用已在 if/while 中提取，这里只返回变量本身
            "++" => operand,
            "--" => operand,
            _ => $"{pre.OperatorToken.Text}{operand}"
        };
    }

    private string TranspilePostfixUnary(PostfixUnaryExpressionSyntax post)
    {
        // null-forgiving operator (!) - 直接返回操作数，忽略 !
        if (post.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            return TranspileExpression(post.Operand);

        var operand = TranspileExpression(post.Operand);
        return post.OperatorToken.Text switch
        {
            // x++ 和 x--：后缀形式需要返回原值，但副作用已提取
            // 这里返回变量本身（简化处理）
            "++" => operand,
            "--" => operand,
            _ => $"{operand}{post.OperatorToken.Text}"
        };
    }

    private string TranspileAssignment(AssignmentExpressionSyntax assign)
    {
        var left = TranspileExpression(assign.Left);
        var right = TranspileExpression(assign.Right);

        var op = assign.Kind() switch
        {
            SyntaxKind.SimpleAssignmentExpression => "=",
            SyntaxKind.AddAssignmentExpression => "+=",
            SyntaxKind.SubtractAssignmentExpression => "-=",
            SyntaxKind.MultiplyAssignmentExpression => "*=",
            SyntaxKind.DivideAssignmentExpression => "/=",
            SyntaxKind.ModuloAssignmentExpression => "%=",
            SyntaxKind.AndAssignmentExpression => "&=",
            SyntaxKind.OrAssignmentExpression => "|=",
            SyntaxKind.ExclusiveOrAssignmentExpression => "^=",
            SyntaxKind.LeftShiftAssignmentExpression => "<<=",
            SyntaxKind.RightShiftAssignmentExpression => ">>=",
            _ => assign.OperatorToken.Text
        };

        return $"{left} {op} {right}";
    }

    private string TranspileConditional(ConditionalExpressionSyntax cond)
    {
        var whenTrue = TranspileExpression(cond.WhenTrue);
        var whenFalse = TranspileExpression(cond.WhenFalse);
        var condition = TranspileExpression(cond.Condition);
        // 注意：TFWR 不支持三元表达式，这里生成的代码可能无法运行
        // 建议在 C# 中用 if-else 语句替代三元表达式
        return $"({whenTrue} if {condition} else {whenFalse})# WARNING: TFWR may not support ternary";
    }

    private string TranspileCast(CastExpressionSyntax cast)
    {
        var inner = TranspileExpression(cast.Expression);
        var typeName = cast.Type.ToString();

        // TFWR 的 Python 不支持类型转换函数（int/float/str/bool）
        // 只有 str() 用于字符串转换时保留（如果 TFWR 支持的话）
        // 其他所有类型转换都忽略，Python 是动态类型的
        return typeName switch
        {
            // 暂时保留 str() 转换（可能在字符串拼接时需要）
            // 如果 TFWR 也不支持 str()，可以将此行也改为 => inner
            "string" => $"str({inner})",

            // 所有其他转换都忽略
            _ => inner
        };
    }

    private string TranspileObjectCreation(ObjectCreationExpressionSyntax obj)
    {
        var typeName = obj.Type.ToString();

        if (typeName.StartsWith("List<") || typeName == "List")
            return obj.Initializer != null ? TranspileInitializer(obj.Initializer) : "[]";

        if (typeName.StartsWith("Dictionary<") || typeName.StartsWith("Dict"))
            return obj.Initializer != null ? TranspileInitializer(obj.Initializer) : "{}";

        if (typeName.StartsWith("HashSet<"))
            return obj.Initializer != null ? $"set({TranspileInitializer(obj.Initializer)})" : "set()";

        if (typeName == "Random")
            return "None";

        // Tuple<T1, T2, ...> → (arg1, arg2, ...)
        if (typeName.StartsWith("Tuple<") || typeName.StartsWith("ValueTuple<"))
        {
            if (obj.ArgumentList != null && obj.ArgumentList.Arguments.Count > 0)
            {
                var tupleItems = string.Join(", ", obj.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression)));
                return $"({tupleItems})";
            }
            return "()";
        }

        var args = obj.ArgumentList != null
        ? string.Join(", ", obj.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression))) : "";
        return $"{typeName}({args})";
    }

    private string TranspileArrayCreation(ArrayCreationExpressionSyntax arr)
    {
        if (arr.Initializer != null)
            return TranspileInitializer(arr.Initializer);
        if (arr.Type.RankSpecifiers.Count > 0 && arr.Type.RankSpecifiers[0].Sizes.Count > 0)
        {
            var size = TranspileExpression(arr.Type.RankSpecifiers[0].Sizes[0]);
            return $"[None] * {size}";
        }
        return "[]";
    }

    private string TranspileImplicitArrayCreation(ImplicitArrayCreationExpressionSyntax arr)
    {
        return TranspileInitializer(arr.Initializer);
    }

    private string TranspileInitializer(InitializerExpressionSyntax init)
    {
        var items = init.Expressions.Select(TranspileExpression);

        if (init.IsKind(SyntaxKind.ComplexElementInitializerExpression))
        {
            var exprs = init.Expressions.ToList();
            if (exprs.Count == 2)
                return $"{TranspileExpression(exprs[0])}: {TranspileExpression(exprs[1])}";
        }

        if (init.Parent is ObjectCreationExpressionSyntax objCreate)
        {
            var typeName = objCreate.Type.ToString();
            if (typeName.StartsWith("Dictionary<") || typeName.StartsWith("Dict"))
            {
                var entries = init.Expressions.Select(e =>
                {
                    if (e is InitializerExpressionSyntax sub && sub.Expressions.Count == 2)
                        return $"{TranspileExpression(sub.Expressions[0])}: {TranspileExpression(sub.Expressions[1])}";
                    return TranspileExpression(e);
                });
                return $"{{{string.Join(", ", entries)}}}";
            }
        }

        return $"[{string.Join(", ", items)}]";
    }

    private string TranspileCollectionExpression(CollectionExpressionSyntax coll)
    {
        var items = coll.Elements.Select(e =>
              {
                  if (e is ExpressionElementSyntax exprElem)
                      return TranspileExpression(exprElem.Expression);
                  return e.ToString();
              });
        return $"[{string.Join(", ", items)}]";
    }

    private string TranspileInterpolatedString(InterpolatedStringExpressionSyntax interp)
    {
        var parts = interp.Contents.Select(c => c switch
        {
            InterpolatedStringTextSyntax text => text.TextToken.Text,
            InterpolationSyntax hole => $"\" + str({TranspileExpression(hole.Expression)}) + \"",
            _ => c.ToString()
        });
        return $"\"{string.Join("", parts)}\"";
    }

    private string TranspileElementAccess(ElementAccessExpressionSyntax elem)
    {
        var obj = TranspileExpression(elem.Expression);
        var args = string.Join(", ", elem.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression)));
        return $"{obj}[{args}]";
    }

    private string TranspileTuple(TupleExpressionSyntax tuple)
    {
        var items = tuple.Arguments.Select(a => TranspileExpression(a.Expression));
        var tupleStr = string.Join(", ", items);

        // 单元素元组需要逗号：(1,)
        // 多元素元组：(1, 2) 或直接 1, 2（在赋值/返回上下文中）
        if (tuple.Arguments.Count == 1)
            return $"({tupleStr},)";

        return $"({tupleStr})";
    }

    private string TranspileIsPattern(IsPatternExpressionSyntax isPat)
    {
        var expr = TranspileExpression(isPat.Expression);
        return isPat.Pattern switch
        {
            ConstantPatternSyntax cp when cp.Expression.IsKind(SyntaxKind.NullLiteralExpression)
            => $"{expr} is None",
            UnaryPatternSyntax { OperatorToken.Text: "not", Pattern: ConstantPatternSyntax { Expression: var e } }
            when e.IsKind(SyntaxKind.NullLiteralExpression)
            => $"{expr} is not None",
            _ => $"# is pattern: {isPat}"
        };
    }

    private static string TranspileTypeName(TypeSyntax type) => type.ToString();

    private string TranspileDeclarationExpression(DeclarationExpressionSyntax declExpr)
    {
        // 处理像 var (x, y) 这样的解构声明表达式
        if (declExpr.Designation is ParenthesizedVariableDesignationSyntax parenDesignation)
        {
            return BuildDeconstructionPattern(parenDesignation);
        }
        else if (declExpr.Designation is SingleVariableDesignationSyntax single)
        {
            return ToSnakeCase(single.Identifier.Text);
        }

        return $"/* {declExpr} */";
    }
}
