namespace Verifex.Parsing;

public enum TokenType : byte
{
    SOF,
    EOF,
    Unknown,
    Let,
    Mut,
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
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    EqualEqual,
    NotEqual,
    And,
    Or,
    Not,
    BitwiseAnd,
    BitwiseOr,
    Bool,
    If,
    Else,
}

public static class TokenTypeExtensions
{
    public static string ToSimpleString(this TokenType token)
    {
        return token switch
        {
            TokenType.SOF => "[SOF]",
            TokenType.EOF => "[EOF]",
            TokenType.Unknown => "[UNKNOWN]",
            TokenType.Let => "let",
            TokenType.Mut => "mut",
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
            TokenType.GreaterThan => ">",
            TokenType.LessThan => "<",
            TokenType.GreaterThanOrEqual => ">=",
            TokenType.LessThanOrEqual => "<=",
            TokenType.EqualEqual => "==",
            TokenType.NotEqual => "!=",
            TokenType.And => "&&",
            TokenType.Or => "||",
            TokenType.Not => "!",
            TokenType.BitwiseAnd => "&",
            TokenType.BitwiseOr => "|",
            TokenType.Bool => "bool",
            TokenType.If => "if",
            TokenType.Else => "else",
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, null)
        };
    }
    
    public static bool IsBoolOp(this TokenType token) => token is TokenType.And or TokenType.Or;
    
    public static bool IsComparisonOp(this TokenType token)
        => token is TokenType.GreaterThan or TokenType.LessThan or TokenType.GreaterThanOrEqual
            or TokenType.LessThanOrEqual or TokenType.EqualEqual or TokenType.NotEqual;
}