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
    While,
    Type,
    Where,
    Struct,
    Dot,
    DotDot,
    FnStatic,
    OrKeyword,
    Is,
    LeftBracket,
    RightBracket,
    Hashtag,
    Archetype,
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
            TokenType.While => "while",
            TokenType.Type => "type",
            TokenType.Where => "where",
            TokenType.Struct => "struct",
            TokenType.Dot => ".",
            TokenType.DotDot => "..",
            TokenType.FnStatic => "fn!",
            TokenType.OrKeyword => "or",
            TokenType.Is => "is",
            TokenType.LeftBracket => "[",
            TokenType.RightBracket => "]",
            TokenType.Hashtag => "#",
            TokenType.Archetype => "archetype",
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, null)
        };
    }
    
    public static bool IsBoolOp(this TokenType token) => token is TokenType.And or TokenType.Or or TokenType.Not;
    
    public static bool IsComparisonOp(this TokenType token)
        => token is TokenType.GreaterThan or TokenType.LessThan or TokenType.GreaterThanOrEqual
            or TokenType.LessThanOrEqual or TokenType.EqualEqual or TokenType.NotEqual;
    
    public static bool IsArithmeticOp(this TokenType token)
        => token is TokenType.Plus or TokenType.Minus or TokenType.Star or TokenType.Slash;
}

