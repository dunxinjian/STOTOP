namespace STOTOP.Module.Finance.Services.FormulaEngine;

/// <summary>
/// 公式引擎实现，遍历AST求值
/// </summary>
public class FormulaEngineImpl : IFormulaEngine
{
    public decimal Evaluate(string formula, FormulaContext context)
    {
        if (string.IsNullOrWhiteSpace(formula))
            return 0m;

        try
        {
            var tokenizer = new FormulaTokenizer(formula);
            var tokens = tokenizer.Tokenize();
            var parser = new FormulaParser(tokens);
            var ast = parser.Parse();
            return EvaluateNode(ast, context);
        }
        catch (FormulaException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FormulaException($"公式求值失败: {ex.Message}");
        }
    }

    public bool Validate(string formula, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(formula))
        {
            error = "公式不能为空";
            return false;
        }

        try
        {
            var tokenizer = new FormulaTokenizer(formula);
            var tokens = tokenizer.Tokenize();
            var parser = new FormulaParser(tokens);
            parser.Parse();
            return true;
        }
        catch (FormulaException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = $"公式验证失败: {ex.Message}";
            return false;
        }
    }

    private decimal EvaluateNode(AstNode node, FormulaContext context)
    {
        switch (node)
        {
            case NumberNode num:
                return num.Value;

            case UnaryMinusNode unary:
                return -EvaluateNode(unary.Operand, context);

            case BinaryOpNode binary:
                var left = EvaluateNode(binary.Left, context);
                var right = EvaluateNode(binary.Right, context);
                return binary.Op switch
                {
                    "+" => left + right,
                    "-" => left - right,
                    "*" => left * right,
                    "/" => right == 0 ? 0m : left / right, // 除零保护
                    _ => throw new FormulaException($"不支持的运算符: {binary.Op}")
                };

            case ComparisonNode comp:
                var cLeft = EvaluateNode(comp.Left, context);
                var cRight = EvaluateNode(comp.Right, context);
                bool result = comp.Op switch
                {
                    ">" => cLeft > cRight,
                    "<" => cLeft < cRight,
                    ">=" => cLeft >= cRight,
                    "<=" => cLeft <= cRight,
                    "==" => cLeft == cRight,
                    "!=" => cLeft != cRight,
                    _ => throw new FormulaException($"不支持的比较运算符: {comp.Op}")
                };
                return result ? 1m : 0m;

            case FunctionCallNode func:
                return EvaluateFunction(func, context);

            case IfNode ifNode:
                var condition = EvaluateNode(ifNode.Condition, context);
                return condition != 0m
                    ? EvaluateNode(ifNode.TrueExpr, context)
                    : EvaluateNode(ifNode.FalseExpr, context);

            case ItemRefNode itemRef:
                if (context.ItemAmounts.TryGetValue(itemRef.ItemName, out var itemVal))
                    return itemVal;
                return 0m; // 引用的项目不存在或未计算，默认 0

            default:
                throw new FormulaException($"不支持的AST节点类型: {node.GetType().Name}");
        }
    }

    private decimal EvaluateFunction(FunctionCallNode func, FormulaContext context)
    {
        switch (func.FunctionName)
        {
            case "SUM":
                return EvalSum(func.Arguments, context);

            case "ROW":
                return EvalRow(func.Arguments, context);

            case "ACCOUNT":
                return EvalAccount(func.Arguments, context);

            case "ABS":
                if (func.Arguments.Count != 1)
                    throw new FormulaException("ABS函数需要1个参数");
                return Math.Abs(EvaluateNode(func.Arguments[0], context));

            case "MAX":
                if (func.Arguments.Count < 2)
                    throw new FormulaException("MAX函数至少需要2个参数");
                return func.Arguments.Max(a => EvaluateNode(a, context));

            case "MIN":
                if (func.Arguments.Count < 2)
                    throw new FormulaException("MIN函数至少需要2个参数");
                return func.Arguments.Min(a => EvaluateNode(a, context));

            default:
                throw new FormulaException($"不支持的函数: {func.FunctionName}");
        }
    }

    /// <summary>
    /// SUM(5001, 5051) — 对参数中的每个科目编码前缀，从context.AccountAmounts中累加所有匹配项
    /// 参数可以是数字字面量（作为科目编码前缀），也可以是表达式
    /// </summary>
    private decimal EvalSum(List<AstNode> args, FormulaContext context)
    {
        if (args.Count == 0)
            throw new FormulaException("SUM函数至少需要1个参数");

        decimal total = 0m;
        foreach (var arg in args)
        {
            // 如果参数是数字字面量，作为科目编码前缀查询
            if (arg is NumberNode numArg)
            {
                string prefix = ((int)numArg.Value).ToString();
                total += GetAmountByPrefix(prefix, context);
            }
            else
            {
                // 否则计算表达式值
                total += EvaluateNode(arg, context);
            }
        }
        return total;
    }

    /// <summary>
    /// ROW(1) — 从context.RowResults中获取行号对应的已计算值
    /// </summary>
    private decimal EvalRow(List<AstNode> args, FormulaContext context)
    {
        if (args.Count != 1)
            throw new FormulaException("ROW函数需要1个参数");

        var rowNum = (int)EvaluateNode(args[0], context);
        return context.RowResults.TryGetValue(rowNum, out var val) ? val : 0m;
    }

    /// <summary>
    /// ACCOUNT(1601) — 从context.AccountAmounts中获取单个科目编码前缀的金额
    /// </summary>
    private decimal EvalAccount(List<AstNode> args, FormulaContext context)
    {
        if (args.Count != 1)
            throw new FormulaException("ACCOUNT函数需要1个参数");

        if (args[0] is NumberNode numArg)
        {
            string prefix = ((int)numArg.Value).ToString();
            return GetAmountByPrefix(prefix, context);
        }

        throw new FormulaException("ACCOUNT函数参数必须是科目编码数字");
    }

    /// <summary>
    /// 按前缀匹配累加金额
    /// </summary>
    private static decimal GetAmountByPrefix(string prefix, FormulaContext context)
    {
        decimal total = 0m;
        foreach (var kv in context.AccountAmounts)
        {
            if (kv.Key.StartsWith(prefix))
                total += kv.Value;
        }
        return total;
    }
}
