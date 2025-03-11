namespace Verifex.Parsing;

public readonly struct Token(TokenType type, Range range)
{
    public readonly TokenType Type = type;
    public readonly Range Range = range;

    public override string ToString() => $"[{Type}]";
}