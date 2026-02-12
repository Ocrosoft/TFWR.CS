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
            ImplicitObjectCreationExpressionSyntax implicitObj => TranspileImplicitObjectCreation(implicitObj),
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

        // 位运算符警告
        if (bin.IsKind(SyntaxKind.BitwiseAndExpression) || 
            bin.IsKind(SyntaxKind.BitwiseOrExpression) ||
            bin.IsKind(SyntaxKind.ExclusiveOrExpression) ||
            bin.IsKind(SyntaxKind.LeftShiftExpression) ||
            bin.IsKind(SyntaxKind.RightShiftExpression))
        {
            var lineNumber = bin.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Console.WriteLine($"[警告] 位运算符 ({op}) 游戏可能不支持 at line {lineNumber}");
        }

        // Null coalescing: a ?? b
        // 在表达式上下文中无法生成 if-else 语句，输出警告
        if (bin.IsKind(SyntaxKind.CoalesceExpression))
        {
            var lineNumber = bin.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Console.WriteLine($"[警告] null coalescing 操作符 (??) 无法在表达式中转译，请改用 if-else 语句 at line {lineNumber}");
            return $"/* UNSUPPORTED: {left} ?? {right} - use if-else statement */";
        }

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
        
        // 按位取反警告
        if (pre.OperatorToken.Text == "~")
        {
            var lineNumber = pre.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Console.WriteLine($"[警告] 按位取反运算符 (~) 游戏可能不支持 at line {lineNumber}");
        }
    
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

        // 位赋值运算符警告
        if (assign.IsKind(SyntaxKind.AndAssignmentExpression) ||
            assign.IsKind(SyntaxKind.OrAssignmentExpression) ||
            assign.IsKind(SyntaxKind.ExclusiveOrAssignmentExpression) ||
            assign.IsKind(SyntaxKind.LeftShiftAssignmentExpression) ||
            assign.IsKind(SyntaxKind.RightShiftAssignmentExpression))
        {
            var lineNumber = assign.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Console.WriteLine($"[警告] 位赋值运算符 ({op}) 游戏可能不支持 at line {lineNumber}");
        }

        return $"{left} {op} {right}";
    }

    private string TranspileConditional(ConditionalExpressionSyntax cond)
    {
        var lineNumber = cond.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        Console.WriteLine($"[警告] 三元条件表达式 (? :) 不支持，请改用 if-else 语句 at line {lineNumber}");

        var whenTrue = TranspileExpression(cond.WhenTrue);
        var whenFalse = TranspileExpression(cond.WhenFalse);
        var condition = TranspileExpression(cond.Condition);

        return $"/* UNSUPPORTED: {condition} ? {whenTrue} : {whenFalse} - use if-else statement */";
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
        {
            // HashSet<T>() → set() 或 {elem1, elem2, ...}
            if (obj.Initializer != null)
            {
                // HashSet<T> { 1, 2, 3 } → {1, 2, 3}
                var items = obj.Initializer.Expressions.Select(TranspileExpression);
                return $"{{{string.Join(", ", items)}}}";
            }
            return "set()";
        }

        if (typeName == "Random")
            return "None";

        // Tuple<T1, T2, ...> 或 KeyValuePair<K, V> → (arg1, arg2, ...)
        if (typeName.StartsWith("Tuple<") || typeName.StartsWith("ValueTuple<") || typeName.StartsWith("KeyValuePair<"))
        {
            if (obj.ArgumentList != null && obj.ArgumentList.Arguments.Count > 0)
            {
                var tupleItems = string.Join(", ", obj.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression)));
                return $"({tupleItems})";
            }
            return "()";
        }

        // 其他对象创建：保留类型名和参数（可能是自定义类）
        var args = obj.ArgumentList != null
          ? string.Join(", ", obj.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression))) : "";
        return $"{typeName}({args})";
    }

    /// <summary>
    /// 处理隐式对象创建表达式 new(...)
    /// </summary>
    private string TranspileImplicitObjectCreation(ImplicitObjectCreationExpressionSyntax implicitObj)
    {
        // 尝试从父节点推断类型
        var inferredType = InferTypeFromContext(implicitObj);

        // 根据推断的类型决定输出
        if (inferredType != null)
        {
            if (inferredType.StartsWith("List<") || inferredType == "List")
            {
                // List<T> → []
                if (implicitObj.Initializer != null)
                    return TranspileInitializer(implicitObj.Initializer);
                return "[]";
            }
            else if (inferredType.StartsWith("Dictionary<") || inferredType.StartsWith("Dict"))
            {
                // Dictionary<K, V> → {}
                if (implicitObj.Initializer != null)
                    return TranspileInitializer(implicitObj.Initializer);
                return "{}";
            }
            else if (inferredType.StartsWith("HashSet<"))
            {
                // HashSet<T> → set()
                if (implicitObj.Initializer != null)
                    return $"set({TranspileInitializer(implicitObj.Initializer)})";
                return "set()";
            }
        }

        // 对于其他类型（Tuple, KeyValuePair, 自定义类等）或无法推断的情况
        // 转换为元组 (arg1, arg2, ...)
        if (implicitObj.ArgumentList != null && implicitObj.ArgumentList.Arguments.Count > 0)
        {
            var args = string.Join(", ", implicitObj.ArgumentList.Arguments.Select(a => TranspileExpression(a.Expression)));
            return $"({args})";
        }

        // 如果有初始化器，使用初始化器
        if (implicitObj.Initializer != null)
            return TranspileInitializer(implicitObj.Initializer);

        // 默认：空元组
        return "()";
    }

    /// <summary>
    /// 从上下文推断隐式对象创建的类型
    /// </summary>
    private static string? InferTypeFromContext(ImplicitObjectCreationExpressionSyntax implicitObj)
    {
        var parent = implicitObj.Parent;

        // 情况1: 变量声明初始化器 var x = new();
        if (parent is EqualsValueClauseSyntax equalsValue &&
            equalsValue.Parent is VariableDeclaratorSyntax declarator &&
            declarator.Parent is VariableDeclarationSyntax declaration)
        {
            return declaration.Type.ToString();
        }

        // 情况2: 赋值表达式右侧 x = new();
        if (parent is AssignmentExpressionSyntax assignment)
        {
            // 尝试从左侧推断类型（这需要符号信息，我们简化处理）
            // 暂时无法准确推断
            return null;
        }

        // 情况3: 方法参数 Method(new());
        if (parent is ArgumentSyntax argument &&
            argument.Parent is ArgumentListSyntax argList &&
            argList.Parent is InvocationExpressionSyntax invocation)
        {
            // 需要方法签名信息，暂时无法准确推断
            return null;
        }

        // 情况4: 集合初始化器 [new(), new()]
        if (parent is ExpressionElementSyntax expressionElement &&
            expressionElement.Parent is CollectionExpressionSyntax collectionExpr &&
            collectionExpr.Parent is EqualsValueClauseSyntax equalsValue2 &&
            equalsValue2.Parent is VariableDeclaratorSyntax declarator2 &&
            declarator2.Parent is VariableDeclarationSyntax declaration2)
        {
            var collectionType = declaration2.Type.ToString();
            // List<T> 或 T[] → 提取元素类型 T
            if (collectionType.StartsWith("List<"))
            {
                var elementType = collectionType.Substring(5, collectionType.Length - 6);
                return elementType;
            }
            return null;
        }

        return null;
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
        // 尝试从父节点推断集合类型
        var inferredType = InferCollectionTypeFromContext(coll);

        // 如果是空集合表达式 []
        if (coll.Elements.Count == 0)
        {
            // 如果无法推断类型，输出警告
            if (inferredType == null)
            {
                var lineNumber = coll.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                Console.WriteLine($"[警告] 无法推断集合表达式 [] 的目标类型，默认转换为空列表 at line {lineNumber}");
                return "[]  # WARNING: Cannot infer type, defaulting to list";
            }

            // 根据推断的类型返回相应的空集合
            if (inferredType.StartsWith("Dictionary<") || inferredType.StartsWith("Dict"))
                return "{}";
            if (inferredType.StartsWith("HashSet<"))
                return "set()";

            // 默认：空列表
            return "[]";
        }

        // 非空集合表达式：生成元素列表
        var items = coll.Elements.Select(e =>
        {
            if (e is ExpressionElementSyntax exprElem)
                return TranspileExpression(exprElem.Expression);
            return e.ToString();
        });

        // 如果无法推断类型，输出警告
        if (inferredType == null)
        {
            var lineNumber = coll.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Console.WriteLine($"[警告] 无法推断集合表达式的目标类型，默认转换为列表 at line {lineNumber}");
            return $"[{string.Join(", ", items)}]  # WARNING: Cannot infer type, defaulting to list";
        }

        // 对于 HashSet，使用集合字面量 {elem1, elem2, ...}
        if (inferredType.StartsWith("HashSet<"))
        {
            return $"{{{string.Join(", ", items)}}}";
        }

        // 对于 Dictionary，需要特殊处理（虽然集合表达式语法不常用于 Dictionary）
        if (inferredType.StartsWith("Dictionary<") || inferredType.StartsWith("Dict"))
        {
            var lineNumber = coll.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Console.WriteLine($"[警告] Dictionary 不建议使用集合表达式语法，推荐使用 new Dictionary<K,V>() at line {lineNumber}");
            // Dictionary 集合表达式通常不直接支持，但如果有，应该生成 {}
            // 这里暂时返回列表，实际使用中应避免这种写法
            return $"[{string.Join(", ", items)}]  # WARNING: Dictionary with collection expression is not recommended";
        }

        return $"[{string.Join(", ", items)}]";
    }

    /// <summary>
    /// 从上下文推断集合表达式的目标类型
    /// </summary>
    private static string? InferCollectionTypeFromContext(CollectionExpressionSyntax coll)
    {
        var parent = coll.Parent;

        // 情况1: 变量声明初始化器 Type x = [];
        if (parent is EqualsValueClauseSyntax equalsValue &&
            equalsValue.Parent is VariableDeclaratorSyntax declarator &&
            declarator.Parent is VariableDeclarationSyntax declaration)
        {
            return declaration.Type.ToString();
        }

        // 情况2: 赋值表达式右侧 x = [];
        // 尝试从左侧标识符推断类型（需要向上查找变量声明）
        if (parent is AssignmentExpressionSyntax assignment)
        {
            // 如果左侧是简单标识符，尝试查找其声明
            if (assignment.Left is IdentifierNameSyntax identifier)
            {
                var varName = identifier.Identifier.Text;
                var inferredType = FindVariableDeclarationType(coll, varName);
                if (inferredType != null)
                {
                    return inferredType;
                }
            }
            return null;
        }

        // 情况3: 方法参数 Method([]);
        if (parent is ArgumentSyntax argument &&
            argument.Parent is ArgumentListSyntax argList &&
            argList.Parent is InvocationExpressionSyntax invocation)
        {
            // 需要方法签名信息，暂时无法准确推断
            return null;
        }

        // 情况4: return [];
        // 尝试从方法返回类型推断
        if (parent is ReturnStatementSyntax)
        {
            var inferredType = FindMethodReturnType(coll);
            if (inferredType != null)
            {
                return inferredType;
            }
            return null;
        }

        return null;
    }

    /// <summary>
    /// 查找变量的声明类型（向上遍历语法树查找声明）
    /// </summary>
    private static string? FindVariableDeclarationType(SyntaxNode startNode, string variableName)
    {
        var currentNode = startNode.Parent;

        while (currentNode != null)
        {
            // 在当前作用域中查找变量声明
            var declarations = currentNode.DescendantNodes()
     .OfType<VariableDeclarationSyntax>()
                .Where(d => d.Variables.Any(v => v.Identifier.Text == variableName));

            foreach (var declaration in declarations)
            {
                // 确保声明在赋值之前（通过位置判断）
                if (declaration.SpanStart < startNode.SpanStart)
                {
                    return declaration.Type.ToString();
                }
            }

            // 向上移动到父节点
            currentNode = currentNode.Parent;
        }

        return null;
    }

    /// <summary>
    /// 查找包含此 return 语句的方法的返回类型
    /// </summary>
    private static string? FindMethodReturnType(SyntaxNode startNode)
    {
        var currentNode = startNode.Parent;

        while (currentNode != null)
        {
            // 检查是否是方法声明
            if (currentNode is MethodDeclarationSyntax method)
            {
                return method.ReturnType.ToString();
            }

            // 检查是否是局部函数
            if (currentNode is LocalFunctionStatementSyntax localFunc)
            {
                return localFunc.ReturnType.ToString();
            }

            // 检查是否是 Lambda 表达式（虽然不支持，但为完整性考虑）
            if (currentNode is SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax)
            {
                // Lambda 的返回类型无法从语法树获取
                return null;
            }

            currentNode = currentNode.Parent;
        }

        return null;
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

    private static string TranspileDeclarationExpression(DeclarationExpressionSyntax declExpr)
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
