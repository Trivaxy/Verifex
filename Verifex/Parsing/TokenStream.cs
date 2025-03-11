namespace Verifex.Parsing;

public class TokenStream
{
    private ReadOnlyMemory<char> _source;
    private int _current;
    private Token _nextToken;

    public TokenStream(string source)
    {
        _source = source.AsMemory();
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
        if (_current >= _source.Length)
            return new Token(TokenType.EOF, _source.Length.._source.Length);
        
        SkipWhitespace();
        
        var remaining = _source.Span;
        var first = remaining[_current];
        var start = _current;
        
        if (char.IsDigit(first))
        {
            ConsumeDigits();
            
            // if there is no decimal, no fractional part - return
            if (_current >= _source.Length || _source.Span[_current] != '.')
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

        switch (first)
        {
            case '=': return new Token(TokenType.Equals, _current..++_current);
            case ';': return new Token(TokenType.Semicolon, _current..++_current);
            case ':': return new Token(TokenType.Colon, _current..++_current);
            case ',': return new Token(TokenType.Comma, _current..++_current);
            case '+': return new Token(TokenType.Plus, _current..++_current);
            case '-':
                if (_current + 1 < _source.Length && remaining[_current + 1] == '>')
                {
                    _current += 2;
                    return new Token(TokenType.Arrow, start.._current);
                }
                
                return new Token(TokenType.Minus, _current..++_current);
            case '*': return new Token(TokenType.Star, _current..++_current);
            case '/': return new Token(TokenType.Slash, _current..++_current);
            case '(': return new Token(TokenType.LeftParenthesis, _current..++_current);
            case ')': return new Token(TokenType.RightParenthesis, _current..++_current);
            case '{': return new Token(TokenType.LeftCurlyBrace, _current..++_current);
            case '}': return new Token(TokenType.RightCurlyBrace, _current..++_current);
        }
        
        ConsumeIdentifier();

        switch (_source[start.._current].Span)
        {
            case "let": return new Token(TokenType.Let, start.._current);
            case "fn": return new Token(TokenType.Fn, start.._current);
        }
        
        if (start != _current)
            return new Token(TokenType.Identifier, start.._current);
        
        return new Token(TokenType.Unknown, start.._current);
    }

    private void ConsumeDigits()
    {
        var remaining = _source.Span;

        while (_current < remaining.Length && char.IsDigit(remaining[_current]))
            _current++;
    }

    private void ConsumeIdentifier()
    {
        var remaining = _source.Span;

        while (_current < remaining.Length && char.IsAsciiLetterOrDigit(remaining[_current]))
            _current++;
    }

    private void ConsumeString()
    {
        var remaining = _source.Span;
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

    private void SkipWhitespace()
    {
        var remaining = _source.Span;

        while (_current < remaining.Length && char.IsWhiteSpace(remaining[_current]))
            _current++;
    }
}
