namespace Verifex;

public class TokenStream
{
    private ReadOnlyMemory<char> _source;
    private int _current;
    private Token _currentToken;
    private Token _nextToken;

    public TokenStream(string source)
    {
        _source = source.AsMemory();
        _current = 0;
        _currentToken = new Token(TokenType.SOF, 0, 1);
        _nextToken = FetchNext();
    }

    public Token Next()
    {
        _currentToken = _nextToken;
        _nextToken = FetchNext();

        return _currentToken;
    }

    public Token Peek() => _nextToken;

    private Token FetchNext()
    {
        if (_current >= _source.Length)
            return new Token(TokenType.EOF, _current, _current);
        
        SkipWhitespace();
        
        var remaining = _source.Span;
        var first = remaining[_current];
        var start = _current;
        
        if (char.IsDigit(first))
        {
            ConsumeDigits();
            return new Token(TokenType.Number, start, _current);
        }
        
        if (first == '"')
        {
            ConsumeString();
            return new Token(TokenType.String, start, _current);
        }

        switch (first)
        {
            case '=': return new Token(TokenType.Equals, _current, ++_current);
            case ';': return new Token(TokenType.Semicolon, _current, ++_current);
        }
        
        ConsumeIdentifier();

        switch (_source[start.._current].Span)
        {
            case "let": return new Token(TokenType.Let, start, _current);
        }
        
        if (start != _current)
            return new Token(TokenType.Identifier, start, _current);
        else
            return new Token(TokenType.Unknown, _current, _current + 1);
    }

    private void ConsumeDigits()
    {
        var remaining = _source.Span;

        while (char.IsDigit(remaining[_current]))
            _current++;
    }

    private void ConsumeIdentifier()
    {
        var remaining = _source.Span;

        while (char.IsAsciiLetterOrDigit(remaining[_current]))
            _current++;
    }

    private void ConsumeString()
    {
        var remaining = _source.Span;

        while (true)
        {
            switch (remaining[_current])
            {
                case '\\':
                    _current += 2;
                    break;
                case '"':
                    _current++;
                    return;
                default:
                    _current++;
                    break;
            }
        }
    }

    private void SkipWhitespace()
    {
        var remaining = _source.Span;

        while (char.IsWhiteSpace(remaining[_current]))
            _current++;
    }
}