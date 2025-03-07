namespace Verifex.Parser;

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
}