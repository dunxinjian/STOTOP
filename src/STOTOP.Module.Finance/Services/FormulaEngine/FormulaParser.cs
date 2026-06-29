namespace STOTOP.Module.Finance.Services.FormulaEngine;

#region AST节点

/// <summary>
/// AST节点基类
/// </summary>
public abstract class AstNode { }

/// <summary>
/// 数字字面量
/// </summary>
public class NumberNode : AstNode
{
    public decimal Value { get; set; }
    public NumberNode(decimal value) => Value = value;
}

/// <summary>
/// 一元取负
/// </summary>
public class UnaryMinusNode : AstNode
{
    public AstNode Operand { get; set; }
    public UnaryMinusNode(AstNode operand) => Operand = operand;
}

/// <summary>
/// 二元运算（+, -, *, /）
/// </summary>
public class BinaryOpNode : AstNode
{
    public AstNode Left { get; set; }
    public AstNode Right { get; set; }
    public string Op { get; set; }
    public BinaryOpNode(AstNode left, string op, AstNode right)
    {
        Left = left;
        Op = op;
        Right = right;
    }
}

/// <summary>
/// 比较运算
/// </summary>
public class ComparisonNode : AstNode
{
    public AstNode Left { get; set; }
    public AstNode Right { get; set; }
    public string Op { get; set; }
    public ComparisonNode(AstNode left, string op, AstNode right)
    {
        Left = left;
        Op = op;
        Right = right;
    }
}

/// <summary>
/// 函数调用（SUM, ROW, ACCOUNT）
/// </summary>
public class FunctionCallNode : AstNode
{
    public string FunctionName { get; set; }
    public List<AstNode> Arguments { get; set; } = new();
    public FunctionCallNode(string name) => FunctionName = name;
}

/// <summary>
/// IF条件表达式
/// </summary>
public class IfNode : AstNode
{
    public AstNode Condition { get; set; }
    public AstNode TrueExpr { get; set; }
    public AstNode FalseExpr { get; set; }
    public IfNode(AstNode condition, AstNode trueExpr, AstNode falseExpr)
    {
        Condition = condition;
        TrueExpr = trueExpr;
        FalseExpr = falseExpr;
    }
}

/// <summary>
/// 项目引用节点 ${项目名称}
/// </summary>
public class ItemRefNode : AstNode
{
    public string ItemName { get; set; }
    public ItemRefNode(string itemName) => ItemName = itemName;
}

#endregion

/// <summary>
/// 递归下降解析器，将Token流解析为AST
/// </summary>
public class FormulaParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public FormulaParser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    private Token Current => _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];

    private Token Consume(TokenType expected)
    {
        var token = Current;
        if (token.Type != expected)
            throw new FormulaException($"期望 {expected}，实际为 {token.Type}（'{token.Value}'），位置 {token.Position}");
        _pos++;
        return token;
    }

    private Token Consume()
    {
        var token = Current;
        _pos++;
        return token;
    }

    /// <summary>
    /// 解析入口
    /// </summary>
    public AstNode Parse()
    {
        var node = ParseExpression();
        if (Current.Type != TokenType.EOF)
            throw new FormulaException($"公式未完全解析，剩余内容位于位置 {Current.Position}");
        return node;
    }

    /// <summary>
    /// 表达式 = 比较表达式
    /// </summary>
    private AstNode ParseExpression()
    {
        return ParseComparison();
    }

    /// <summary>
    /// 比较表达式 = 加减表达式 [ 比较运算符 加减表达式 ]
    /// </summary>
    private AstNode ParseComparison()
    {
        var left = ParseAddSub();

        if (Current.Type is TokenType.GreaterThan or TokenType.LessThan
            or TokenType.GreaterThanOrEqual or TokenType.LessThanOrEqual
            or TokenType.Equal or TokenType.NotEqual)
        {
            var op = Consume().Value;
            var right = ParseAddSub();
            return new ComparisonNode(left, op, right);
        }

        return left;
    }

    /// <summary>
    /// 加减表达式 = 乘除表达式 { (+|-) 乘除表达式 }
    /// </summary>
    private AstNode ParseAddSub()
    {
        var left = ParseMulDiv();

        while (Current.Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Consume().Value;
            var right = ParseMulDiv();
            left = new BinaryOpNode(left, op, right);
        }

        return left;
    }

    /// <summary>
    /// 乘除表达式 = 一元表达式 { (*|/) 一元表达式 }
    /// </summary>
    private AstNode ParseMulDiv()
    {
        var left = ParseUnary();

        while (Current.Type is TokenType.Multiply or TokenType.Divide)
        {
            var op = Consume().Value;
            var right = ParseUnary();
            left = new BinaryOpNode(left, op, right);
        }

        return left;
    }

    /// <summary>
    /// 一元表达式 = [-] 主表达式
    /// </summary>
    private AstNode ParseUnary()
    {
        if (Current.Type == TokenType.Minus)
        {
            Consume();
            var operand = ParsePrimary();
            return new UnaryMinusNode(operand);
        }

        return ParsePrimary();
    }

    /// <summary>
    /// 主表达式 = 数字 | 函数调用 | (表达式) | ${项目引用}
    /// </summary>
    private AstNode ParsePrimary()
    {
        var token = Current;

        // 项目引用 ${名称}
        if (token.Type == TokenType.ItemRef)
        {
            Consume();
            return new ItemRefNode(token.Value);
        }

        // 数字
        if (token.Type == TokenType.Number)
        {
            Consume();
            if (decimal.TryParse(token.Value, out var num))
                return new NumberNode(num);
            throw new FormulaException($"无法解析数字 '{token.Value}'，位置 {token.Position}");
        }

        // 标识符（函数调用）
        if (token.Type == TokenType.Identifier)
        {
            var name = token.Value.ToUpperInvariant();
            Consume();

            // IF 特殊处理
            if (name == "IF")
            {
                Consume(TokenType.LeftParen);
                var condition = ParseExpression();
                Consume(TokenType.Comma);
                var trueExpr = ParseExpression();
                Consume(TokenType.Comma);
                var falseExpr = ParseExpression();
                Consume(TokenType.RightParen);
                return new IfNode(condition, trueExpr, falseExpr);
            }

            // 普通函数 SUM, ROW, ACCOUNT 等
            if (Current.Type == TokenType.LeftParen)
            {
                Consume(TokenType.LeftParen);
                var func = new FunctionCallNode(name);

                if (Current.Type != TokenType.RightParen)
                {
                    func.Arguments.Add(ParseExpression());
                    while (Current.Type == TokenType.Comma)
                    {
                        Consume();
                        func.Arguments.Add(ParseExpression());
                    }
                }

                Consume(TokenType.RightParen);
                return func;
            }

            // 裸标识符作为函数名（无括号的情况下不允许）
            throw new FormulaException($"标识符 '{token.Value}' 后缺少 '('，位置 {token.Position}");
        }

        // 括号表达式
        if (token.Type == TokenType.LeftParen)
        {
            Consume();
            var expr = ParseExpression();
            Consume(TokenType.RightParen);
            return expr;
        }

        throw new FormulaException($"意外的Token '{token.Value}'（{token.Type}），位置 {token.Position}");
    }
}
