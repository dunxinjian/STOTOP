namespace STOTOP.Module.Finance.Services.FormulaEngine;

/// <summary>
/// Token类型
/// </summary>
public enum TokenType
{
    Number,
    Identifier,
    Plus,
    Minus,
    Multiply,
    Divide,
    LeftParen,
    RightParen,
    Comma,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Equal,
    NotEqual,
    ItemRef,
    EOF
}

/// <summary>
/// 词法Token
/// </summary>
public class Token
{
    public TokenType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public int Position { get; set; }

    public Token(TokenType type, string value, int position)
    {
        Type = type;
        Value = value;
        Position = position;
    }
}

/// <summary>
/// 词法分析器，将公式字符串解析为Token流
/// </summary>
public class FormulaTokenizer
{
    private readonly string _input;
    private int _pos;

    public FormulaTokenizer(string input)
    {
        _input = input ?? string.Empty;
        _pos = 0;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < _input.Length)
        {
            var ch = _input[_pos];

            if (char.IsWhiteSpace(ch))
            {
                _pos++;
                continue;
            }

            // ${项目名称} 引用
            if (ch == '$' && _pos + 1 < _input.Length && _input[_pos + 1] == '{')
            {
                tokens.Add(ReadItemRef());
                continue;
            }

            if (char.IsDigit(ch) || (ch == '.' && _pos + 1 < _input.Length && char.IsDigit(_input[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            switch (ch)
            {
                case '+':
                    tokens.Add(new Token(TokenType.Plus, "+", _pos++));
                    break;
                case '-':
                    tokens.Add(new Token(TokenType.Minus, "-", _pos++));
                    break;
                case '*':
                    tokens.Add(new Token(TokenType.Multiply, "*", _pos++));
                    break;
                case '/':
                    tokens.Add(new Token(TokenType.Divide, "/", _pos++));
                    break;
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "(", _pos++));
                    break;
                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")", _pos++));
                    break;
                case ',':
                    tokens.Add(new Token(TokenType.Comma, ",", _pos++));
                    break;
                case '>':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    {
                        tokens.Add(new Token(TokenType.GreaterThanOrEqual, ">=", _pos));
                        _pos += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.GreaterThan, ">", _pos++));
                    }
                    break;
                case '<':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    {
                        tokens.Add(new Token(TokenType.LessThanOrEqual, "<=", _pos));
                        _pos += 2;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.LessThan, "<", _pos++));
                    }
                    break;
                case '=':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    {
                        tokens.Add(new Token(TokenType.Equal, "==", _pos));
                        _pos += 2;
                    }
                    else
                    {
                        throw new FormulaException($"位置 {_pos} 处的字符 '=' 无效，是否意在使用 '=='？");
                    }
                    break;
                case '!':
                    if (_pos + 1 < _input.Length && _input[_pos + 1] == '=')
                    {
                        tokens.Add(new Token(TokenType.NotEqual, "!=", _pos));
                        _pos += 2;
                    }
                    else
                    {
                        throw new FormulaException($"位置 {_pos} 处的字符 '!' 无效，是否意在使用 '!='？");
                    }
                    break;
                default:
                    throw new FormulaException($"位置 {_pos} 处的字符 '{ch}' 无法识别");
            }
        }

        tokens.Add(new Token(TokenType.EOF, "", _pos));
        return tokens;
    }

    private Token ReadNumber()
    {
        int start = _pos;
        bool hasDot = false;

        while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.'))
        {
            if (_input[_pos] == '.')
            {
                if (hasDot) break;
                hasDot = true;
            }
            _pos++;
        }

        return new Token(TokenType.Number, _input[start.._pos], start);
    }

    private Token ReadIdentifier()
    {
        int start = _pos;
        while (_pos < _input.Length && (char.IsLetterOrDigit(_input[_pos]) || _input[_pos] == '_'))
        {
            _pos++;
        }
        return new Token(TokenType.Identifier, _input[start.._pos], start);
    }

    /// <summary>
    /// 读取 ${项目名称} 引用
    /// </summary>
    private Token ReadItemRef()
    {
        int start = _pos;
        _pos += 2; // skip ${
        int nameStart = _pos;
        while (_pos < _input.Length && _input[_pos] != '}')
        {
            _pos++;
        }
        if (_pos >= _input.Length)
            throw new FormulaException($"位置 {start} 处的项目引用 '${{' 缺少结束符 '}}'" );
        string name = _input[nameStart.._pos];
        _pos++; // skip }
        return new Token(TokenType.ItemRef, name, start);
    }
}

/// <summary>
/// 公式异常
/// </summary>
public class FormulaException : Exception
{
    public FormulaException(string message) : base(message) { }
}
