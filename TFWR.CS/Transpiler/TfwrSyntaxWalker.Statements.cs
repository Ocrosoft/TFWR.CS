using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TFWR.CS.Transpiler;

/// <summary>
/// TfwrSyntaxWalker 的语句处理部分
/// </summary>
internal partial class TfwrSyntaxWalker
{
    // ========================================================================
    // 语句转译
    // ========================================================================
    private void VisitStatement(StatementSyntax stmt)
    {
        switch (stmt)
        {
            case BlockSyntax block:
                VisitBlockBody(block);
                break;

            case ExpressionStatementSyntax exprStmt:
                VisitExpressionStatement(exprStmt);
                break;

            case LocalDeclarationStatementSyntax localDecl:
                VisitLocalDeclaration(localDecl);
                break;

            case ReturnStatementSyntax ret:
                VisitReturnStatement(ret);
                break;

            case IfStatementSyntax ifStmt:
                VisitIfStatement(ifStmt);
                break;

            case WhileStatementSyntax whileStmt:
                VisitWhileStatement(whileStmt);
                break;

            case ForStatementSyntax forStmt:
                VisitForStatement(forStmt);
                break;

            case ForEachStatementSyntax foreachStmt:
                VisitForEachStatement(foreachStmt);
                break;

            case BreakStatementSyntax:
                builder.AppendLine("break");
                break;

            case ContinueStatementSyntax:
                builder.AppendLine("continue");
                break;

            case DoStatementSyntax doStmt:
                VisitDoStatement(doStmt);
                break;

            case SwitchStatementSyntax switchStmt:
                VisitSwitchStatement(switchStmt);
                break;

            case LocalFunctionStatementSyntax localFunc:
                VisitLocalFunctionStatement(localFunc);
                break;

            case TryStatementSyntax tryStmt:
                WarnUnsupportedStatement(tryStmt, "try-catch-finally");
                // 尝试转译 try 块体中的语句（忽略 catch/finally）
                if (tryStmt.Block.Statements.Count > 0)
                {
                    builder.AppendLine("# WARNING: try-catch-finally not supported, executing try block only");
                    VisitBlockBody(tryStmt.Block);
                }
                break;

            case ThrowStatementSyntax throwStmt:
                WarnUnsupportedStatement(throwStmt, "throw");
                builder.AppendLine($"# UNSUPPORTED: throw");
                break;

            case UsingStatementSyntax usingStmt:
                WarnUnsupportedStatement(usingStmt, "using 语句");
                // 尝试转译 using 块体
                if (usingStmt.Statement != null)
                {
                    builder.AppendLine("# WARNING: using statement not supported, executing body only");
                    VisitStatementAsBody(usingStmt.Statement);
                }
                break;

            case LockStatementSyntax lockStmt:
                WarnUnsupportedStatement(lockStmt, "lock 语句");
                if (lockStmt.Statement != null)
                {
                    builder.AppendLine("# WARNING: lock statement not supported, executing body only");
                    VisitStatementAsBody(lockStmt.Statement);
                }
                break;

            case YieldStatementSyntax yieldStmt:
                WarnUnsupportedStatement(yieldStmt, "yield return/break");
                builder.AppendLine($"# UNSUPPORTED: yield");
                break;

            case ForEachVariableStatementSyntax foreachVarStmt:
                WarnUnsupportedStatement(foreachVarStmt, "foreach 解构（foreach var (x, y) in ...）");
                builder.AppendLine($"# UNSUPPORTED: foreach deconstruction");
                break;

            case GotoStatementSyntax gotoStmt:
                WarnUnsupportedStatement(gotoStmt, "goto");
                builder.AppendLine($"# UNSUPPORTED: goto");
                break;

            case LabeledStatementSyntax labeledStmt:
                WarnUnsupportedStatement(labeledStmt, "标签语句");
                // 转译标签后面的语句
                VisitStatement(labeledStmt.Statement);
                break;

            case CheckedStatementSyntax checkedStmt:
                WarnUnsupportedStatement(checkedStmt, "checked/unchecked");
                VisitBlockBody(checkedStmt.Block);
                break;

            case EmptyStatementSyntax:
                // 空语句，不需要警告
                break;

            default:
                var message = $"无法转译的语句类型: {stmt.Kind()} at line {stmt.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                Console.WriteLine($"[警告] {message}");
                builder.AppendLine($"# TODO: unsupported statement {stmt.Kind()}");
                break;
        }
    }

    private void VisitBlockBody(BlockSyntax block)
    {
        if (block.Statements.Count == 0)
        {
            builder.AppendLine("pass");
            return;
        }

        foreach (var stmt in block.Statements)
            VisitStatement(stmt);
    }

    private new void VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        var expr = node.Expression;

        // i++ → i += 1, i-- → i -= 1
        if (expr is PostfixUnaryExpressionSyntax postfix)
        {
            var operand = TranspileExpression(postfix.Operand);
            var op = postfix.OperatorToken.Text == "++" ? "+=" : "-=";
            builder.AppendLine($"{operand} {op} 1");
            return;
        }
        if (expr is PrefixUnaryExpressionSyntax prefix &&
            (prefix.OperatorToken.Text == "++" || prefix.OperatorToken.Text == "--"))
        {
            var operand = TranspileExpression(prefix.Operand);
            var op = prefix.OperatorToken.Text == "++" ? "+=" : "-=";
            builder.AppendLine($"{operand} {op} 1");
            return;
        }

        builder.AppendLine(TranspileExpression(expr));
    }

    private void VisitLocalDeclaration(LocalDeclarationStatementSyntax node)
    {
        // 首先检查是否有 ParenthesizedVariableDesignationSyntax （元组解构）
        var parenDesignation = node.DescendantNodes()
               .OfType<ParenthesizedVariableDesignationSyntax>()
               .FirstOrDefault();

        if (parenDesignation != null && node.Declaration.Variables.Count > 0)
        {
            var variable = node.Declaration.Variables[0];
            if (variable.Initializer != null)
            {
                // 如果初始化为 MemberAccess 且成员名为 Value，使用其对象部分（例如 companion.Value → companion）
                ExpressionSyntax rhsExpr = variable.Initializer.Value;
                if (rhsExpr is MemberAccessExpressionSyntax rhsMa && rhsMa.Name.Identifier.Text == "Value")
                    rhsExpr = rhsMa.Expression;

                var pattern = BuildDeconstructionPattern(parenDesignation);
                var value = TranspileExpression(rhsExpr);
                builder.AppendLine($"{pattern} = {value}");

                // Record all variables
                foreach (var designation in parenDesignation.DescendantNodesAndSelf()
                       .OfType<SingleVariableDesignationSyntax>())
                {
                    _globalVarsWritten.Add(designation.Identifier.Text);
                }
                return;
            }
        }

        // Check for tuple type syntax: (Type1, Type2) (x, y) = ...
        if (node.Declaration.Type is TupleTypeSyntax)
        {
            var designations = node.DescendantNodes()
                .OfType<SingleVariableDesignationSyntax>()
                .ToList();

            if (designations.Count > 0 && node.Declaration.Variables.Count > 0)
            {
                var variable = node.Declaration.Variables[0];
                if (variable.Initializer != null)
                {
                    // 同样处理 .Value 的情况
                    ExpressionSyntax rhsExpr = variable.Initializer.Value;
                    if (rhsExpr is MemberAccessExpressionSyntax rhsMa && rhsMa.Name.Identifier.Text == "Value")
                        rhsExpr = rhsMa.Expression;

                    var value = TranspileExpression(rhsExpr);
                    var pattern = string.Join(", ", designations.Select(d => ToSnakeCase(d.Identifier.Text)));
                    builder.AppendLine($"{pattern} = {value}");

                    // Record all variables
                    foreach (var designation in designations)
                    {
                        _globalVarsWritten.Add(designation.Identifier.Text);
                    }
                    return;
                }
            }
        }

        // Standard variable declaration
        foreach (var v in node.Declaration.Variables)
        {
            var name = ToSnakeCase(v.Identifier.Text);
            _globalVarsWritten.Add(v.Identifier.Text);

            if (v.Initializer != null)
            {
                var value = TranspileExpression(v.Initializer.Value);
                builder.AppendLine($"{name} = {value}");
            }
            else
            {
                builder.AppendLine($"{name} = None");
            }
        }
    }

    private new void VisitReturnStatement(ReturnStatementSyntax node)
    {
        if (node.Expression != null)
        {
            var expr = node.Expression;

            // 如果返回的是元组表达式，Python 可以省略括号
            // return (1, 2, 3) → return 1, 2, 3
            if (expr is TupleExpressionSyntax tuple)
            {
                var items = tuple.Arguments.Select(a => TranspileExpression(a.Expression));
                builder.AppendLine($"return {string.Join(", ", items)}");
            }
            else
            {
                builder.AppendLine($"return {TranspileExpression(node.Expression)}");
            }
        }
        else
        {
            builder.AppendLine("return");
        }
    }

    private new void VisitIfStatement(IfStatementSyntax node)
    {
        // 特殊处理：如果条件中包含前缀/后缀递增递减，需要先提取
        var condition = node.Condition;
        var hasIncrementDecrement = condition.DescendantNodesAndSelf()
      .OfType<ExpressionSyntax>()
            .Any(e => e is PrefixUnaryExpressionSyntax { OperatorToken.Text: "++" or "--" }
        || e is PostfixUnaryExpressionSyntax { OperatorToken.Text: "++" or "--" });

        if (hasIncrementDecrement)
        {
            // 提取所有前缀递增递减为独立语句
            foreach (var prefix in condition.DescendantNodesAndSelf().OfType<PrefixUnaryExpressionSyntax>())
            {
                if (prefix.OperatorToken.Text is "++" or "--")
                {
                    var op = prefix.OperatorToken.Text == "++" ? "+=" : "-=";
                    var operand = TranspileExpression(prefix.Operand);
                    builder.AppendLine($"{operand} {op} 1");
                }
            }
        }

        builder.AppendLine($"if {TranspileExpression(node.Condition)}:");
        builder.IncreaseIndent();
        VisitStatementAsBody(node.Statement);
        builder.DecreaseIndent();

        if (node.Else != null)
        {
            if (node.Else.Statement is IfStatementSyntax elseIf)
            {
                builder.AppendLine($"elif {TranspileExpression(elseIf.Condition)}:");
                builder.IncreaseIndent();
                VisitStatementAsBody(elseIf.Statement);
                builder.DecreaseIndent();

                // 递归处理 else if 链
                if (elseIf.Else != null)
                {
                    VisitElseClause(elseIf.Else);
                }
            }
            else
            {
                builder.AppendLine("else:");
                builder.IncreaseIndent();
                VisitStatementAsBody(node.Else.Statement);
                builder.DecreaseIndent();
            }
        }
    }

    private new void VisitElseClause(ElseClauseSyntax elseClause)
    {
        if (elseClause.Statement is IfStatementSyntax elseIf)
        {
            builder.AppendLine($"elif {TranspileExpression(elseIf.Condition)}:");
            builder.IncreaseIndent();
            VisitStatementAsBody(elseIf.Statement);
            builder.DecreaseIndent();

            if (elseIf.Else != null)
                VisitElseClause(elseIf.Else);
        }
        else
        {
            builder.AppendLine("else:");
            builder.IncreaseIndent();
            VisitStatementAsBody(elseClause.Statement);
            builder.DecreaseIndent();
        }
    }

    private new void VisitWhileStatement(WhileStatementSyntax node)
    {
        // 特殊处理：如果条件中包含前缀/后缀递增递减，需要先提取
        var condition = node.Condition;
        var prefixOps = condition.DescendantNodesAndSelf()
      .OfType<PrefixUnaryExpressionSyntax>()
              .Where(p => p.OperatorToken.Text is "++" or "--")
      .ToList();

        if (prefixOps.Count > 0)
        {
            // 提取前缀递增递减
            foreach (var prefix in prefixOps)
            {
                var op = prefix.OperatorToken.Text == "++" ? "+=" : "-=";
                var operand = TranspileExpression(prefix.Operand);
                builder.AppendLine($"{operand} {op} 1");
            }
        }

        var conditionExpr = TranspileExpression(node.Condition);
        builder.AppendLine($"while {conditionExpr}:");
        builder.IncreaseIndent();
        VisitStatementAsBody(node.Statement);
        builder.DecreaseIndent();
    }

    private new void VisitForStatement(ForStatementSyntax node)
    {
        // 尝试识别 for (int i = start; i < end; i++ / i += step) 模式 → for i in range(start, end, step)
        if (TryTranspileAsRange(node))
            return;

        // 回退: 用 while 模拟
        if (node.Declaration != null)
        {
            foreach (var v in node.Declaration.Variables)
            {
                var name = ToSnakeCase(v.Identifier.Text);
                var init = v.Initializer != null ? TranspileExpression(v.Initializer.Value) : "0";
                builder.AppendLine($"{name} = {init}");
            }
        }
        foreach (var init in node.Initializers)
            builder.AppendLine(TranspileExpression(init));

        var cond = node.Condition != null ? TranspileExpression(node.Condition) : "True";
        builder.AppendLine($"while {cond}:");
        builder.IncreaseIndent();
        VisitStatementAsBody(node.Statement);
        foreach (var inc in node.Incrementors)
        {
            if (inc is PostfixUnaryExpressionSyntax pf)
            {
                var op2 = pf.OperatorToken.Text == "++" ? "+=" : "-=";
                builder.AppendLine($"{TranspileExpression(pf.Operand)} {op2} 1");
            }
            else if (inc is PrefixUnaryExpressionSyntax pr &&
                (pr.OperatorToken.Text == "++" || pr.OperatorToken.Text == "--"))
            {
                var op2 = pr.OperatorToken.Text == "++" ? "+=" : "-=";
                builder.AppendLine($"{TranspileExpression(pr.Operand)} {op2} 1");
            }
            else
            {
                builder.AppendLine(TranspileExpression(inc));
            }
        }
        builder.DecreaseIndent();
    }

    private bool TryTranspileAsRange(ForStatementSyntax node)
    {
        // for (int i = start; i < end; i++ / i += step)
        if (node.Declaration == null || node.Declaration.Variables.Count != 1) return false;
        if (node.Condition == null || node.Incrementors.Count != 1) return false;

        var v = node.Declaration.Variables[0];
        var varName = ToSnakeCase(v.Identifier.Text);
        var start = v.Initializer != null ? TranspileExpression(v.Initializer.Value) : "0";

        // 条件: i < end 或 i <= end
        string end;
        if (node.Condition is BinaryExpressionSyntax bin)
        {
            if (bin.Left is IdentifierNameSyntax leftId && leftId.Identifier.Text == v.Identifier.Text)
            {
                end = TranspileExpression(bin.Right);
                if (bin.IsKind(SyntaxKind.LessThanOrEqualExpression))
                    end = $"{end} + 1";
                else if (!bin.IsKind(SyntaxKind.LessThanExpression) && !bin.IsKind(SyntaxKind.NotEqualsExpression))
                    return false;
            }
            else
                return false;
        }
        else
            return false;

        // 步长
        var inc = node.Incrementors[0];
        string step;
        if (inc is PostfixUnaryExpressionSyntax { OperatorToken.Text: "++" })
            step = "1";
        else if (inc is PrefixUnaryExpressionSyntax { OperatorToken.Text: "++" })
            step = "1";
        else if (inc is AssignmentExpressionSyntax assign && assign.IsKind(SyntaxKind.AddAssignmentExpression))
            step = TranspileExpression(assign.Right);
        else if (inc is PostfixUnaryExpressionSyntax { OperatorToken.Text: "--" })
            step = "-1";
        else if (inc is PrefixUnaryExpressionSyntax { OperatorToken.Text: "--" })
            step = "-1";
        else
            return false;

        // 优化输出
        if (start == "0" && step == "1")
            builder.AppendLine($"for {varName} in range({end}):");
        else if (step == "1")
            builder.AppendLine($"for {varName} in range({start}, {end}):");
        else
            builder.AppendLine($"for {varName} in range({start}, {end}, {step}):");

        builder.IncreaseIndent();
        VisitStatementAsBody(node.Statement);
        builder.DecreaseIndent();

        return true;
    }

    private new void VisitForEachStatement(ForEachStatementSyntax node)
    {
        var varName = ToSnakeCase(node.Identifier.Text);
        var collection = TranspileExpression(node.Expression);
        builder.AppendLine($"for {varName} in {collection}:");
        builder.IncreaseIndent();
        VisitStatementAsBody(node.Statement);
        builder.DecreaseIndent();
    }

    private new void VisitDoStatement(DoStatementSyntax node)
    {
        // do { ... } while (cond) → while True: ... if not cond: break
        builder.AppendLine("while True:");
        builder.IncreaseIndent();
        VisitStatementAsBody(node.Statement);
        builder.AppendLine($"if not ({TranspileExpression(node.Condition)}):");
        builder.IncreaseIndent();
        builder.AppendLine("break");
        builder.DecreaseIndent();
        builder.DecreaseIndent();
    }

    private new void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        var expr = TranspileExpression(node.Expression);
        bool first = true;
        foreach (var section in node.Sections)
        {
            bool isDefault = section.Labels.Any(l => l is DefaultSwitchLabelSyntax);
            var caseValues = section.Labels
                 .OfType<CaseSwitchLabelSyntax>()
           .Select(l => TranspileExpression(l.Value))
                 .ToList();

            if (isDefault && !first)
            {
                builder.AppendLine("else:");
            }
            else if (caseValues.Count > 0)
            {
                var cond = string.Join(" or ", caseValues.Select(v => $"{expr} == {v}"));
                builder.AppendLine(first ? $"if {cond}:" : $"elif {cond}:");
            }

            first = false;
            builder.IncreaseIndent();
            foreach (var s in section.Statements)
            {
                if (s is BreakStatementSyntax) continue; // switch break 不需要
                VisitStatement(s);
            }
            builder.DecreaseIndent();
        }
    }

    private void VisitStatementAsBody(StatementSyntax stmt)
    {
        if (stmt is BlockSyntax block)
            VisitBlockBody(block);
        else
            VisitStatement(stmt);
    }

    private void WarnUnsupportedStatement(StatementSyntax stmt, string description)
    {
        var lineNumber = stmt.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        Console.WriteLine($"[警告] 不支持的语句: {description} at line {lineNumber}");
    }
}
