namespace Verifex.Parsing;

public enum TokenType : byte
{
    SOF,
    EOF,
    Unknown,
    Let,
    Number,
    Equals,
    Semicolon,
    Colon,
    Comma,
    String,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    LeftParenthesis,
    RightParenthesis,
    LeftCurlyBrace,
    RightCurlyBrace,
    Fn,
    Arrow,
    Return,
}

public static class TokenTypeExtensions
{
    public static string ToSimpleString(this TokenType tokenType)
    {
        return tokenType switch
        {
            TokenType.SOF => "[SOF]",
            TokenType.EOF => "[EOF]",
            TokenType.Unknown => "[UNKNOWN]",
            TokenType.Let => "let",
            TokenType.Number => "number",
            TokenType.Equals => "=",
            TokenType.Semicolon => ";",
            TokenType.Colon => ":",
            TokenType.Comma => ",",
            TokenType.String => "string",
            TokenType.Identifier => "identifier",
            TokenType.Plus => "+",
            TokenType.Minus => "-",
            TokenType.Star => "*",
            TokenType.Slash => "/",
            TokenType.LeftParenthesis => "(",
            TokenType.RightParenthesis => ")",
            TokenType.LeftCurlyBrace => "{",
            TokenType.RightCurlyBrace => "}",
            TokenType.Fn => "fn",
            TokenType.Arrow => "->",
            TokenType.Return => "return",
            _ => throw new ArgumentOutOfRangeException(nameof(tokenType), tokenType, null)
        };
    }
}