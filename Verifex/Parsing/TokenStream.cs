namespace Verifex.Parsing;

public class TokenStream
{
    private readonly ReadOnlyMemory<char> _sourceMemory;
    private int _current;
    private Token _nextToken;

    private static readonly Dictionary<char, TokenType> SingleCharTokens = new()
    {
        ['='] = TokenType.Equals,
        [';'] = TokenType.Semicolon,
        [':'] = TokenType.Colon,
        [','] = TokenType.Comma,
        ['+'] = TokenType.Plus,
        ['-'] = TokenType.Minus,
        ['*'] = TokenType.Star,
        ['/'] = TokenType.Slash,
        ['('] = TokenType.LeftParenthesis,
        [')'] = TokenType.RightParenthesis,
        ['{'] = TokenType.LeftCurlyBrace,
        ['}'] = TokenType.RightCurlyBrace,
        ['>'] = TokenType.GreaterThan,
        ['<'] = TokenType.LessThan,
        ['!'] = TokenType.Not,
        ['&'] = TokenType.BitwiseAnd,
        ['|'] = TokenType.BitwiseOr,
    };

    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["let"] = TokenType.Let,
        ["fn"] = TokenType.Fn,
        ["return"] = TokenType.Return
    };

    public TokenStream(string source)
    {
        _sourceMemory = source.AsMemory();
        _current = 0;
        Current = new Token(TokenType.SOF, 0..0);
        _nextToken = FetchNext();
    }

    public Token Current { get; private set; }

    public Token Next()
    {
        Current = _nextToken;
        _nextToken = FetchNext();

        return Current;
    }

    public Token Peek() => _nextToken;

    private Token FetchNext()
    {
        if (_current >= _sourceMemory.Length)
            return new Token(TokenType.EOF, _sourceMemory.Length.._sourceMemory.Length);

        SkipWhitespace();

        var source = _sourceMemory.Span;
        var first = source[_current];
        var start = _current;

        if (char.IsDigit(first))
        {
            ConsumeDigits();

            // if there is no decimal, no fractional part - return
            if (_current >= source.Length || source[_current] != '.')
                return new Token(TokenType.Number, start.._current);

            _current++;
            ConsumeDigits();

            return new Token(TokenType.Number, start.._current);
        }

        if (first == '"')
        {
            ConsumeString();
            return new Token(TokenType.String, start.._current);
        }

        if (SingleCharTokens.TryGetValue(first, out var tokenType))
        {
            // Check for two-character tokens
            if (_current + 1 < source.Length)
            {
                char second = source[_current + 1];
                TokenType actualToken = tokenType switch
                {
                    TokenType.Minus when second == '>' => TokenType.Arrow,
                    TokenType.GreaterThan when second == '=' => TokenType.GreaterThanOrEqual,
                    TokenType.LessThan when second == '=' => TokenType.LessThanOrEqual,
                    TokenType.Equals when second == '=' => TokenType.EqualEqual,
                    TokenType.Not when second == '=' => TokenType.NotEqual,
                    TokenType.BitwiseAnd when second == '&' => TokenType.And,
                    TokenType.BitwiseOr when second == '|' => TokenType.Or,
                    _ => TokenType.Unknown
                };

                if (actualToken != TokenType.Unknown)
                {
                    _current += 2;
                    return new Token(actualToken, start.._current);
                }
            }

            _current++;
            return new Token(tokenType, start.._current);
        }

        ConsumeIdentifier();

        switch (source[start.._current])
        {
            case "let": return new Token(TokenType.Let, start.._current);
            case "fn": return new Token(TokenType.Fn, start.._current);
            case "return": return new Token(TokenType.Return, start.._current);
        }

        if (start != _current)
            return new Token(TokenType.Identifier, start.._current);

        ConsumeUnknown();
        return new Token(TokenType.Unknown, start.._current);
    }

    private void ConsumeDigits()
    {
        var remaining = _sourceMemory.Span;

        while (_current < remaining.Length && char.IsDigit(remaining[_current]))
            _current++;
    }

    private void ConsumeIdentifier()
    {
        var remaining = _sourceMemory.Span;

        while (_current < remaining.Length && char.IsAsciiLetterOrDigit(remaining[_current]))
            _current++;
    }

    private void ConsumeString()
    {
        var remaining = _sourceMemory.Span;
        _current++; // skip the opening quote

        while (_current < remaining.Length)
        {
            char c = remaining[_current];
            _current++; // move to the next character

            switch (c)
            {
                case '"': return;
                case '\\':
                {
                    // consume the backslash
                    if (_current >= remaining.Length)
                        throw new Exception("Unterminated string literal after escape character");

                    // Just skip the escaped character - actual escape sequence processing
                    // would happen in the parser when it creates the StringLiteralNode
                    _current++;
                    break;
                }
            }
        }

        throw new Exception("Unterminated string literal");
    }

    private void ConsumeUnknown()
    {
        var remaining = _sourceMemory.Span;

        while (_current < remaining.Length && !char.IsAsciiLetterOrDigit(remaining[_current]) &&
               !char.IsWhiteSpace(remaining[_current]) && !SingleCharTokens.ContainsKey(remaining[_current]))
            _current++;
    }

    private void SkipWhitespace()
    {
        var remaining = _sourceMemory.Span;

        while (_current < remaining.Length && char.IsWhiteSpace(remaining[_current]))
            _current++;
    }
}