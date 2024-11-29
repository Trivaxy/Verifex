namespace Verifex;

public readonly struct Token(TokenType type, int start, int end)
{
    public readonly TokenType Type = type;
    public readonly int Start = start;
    public readonly int End = end;

    public override string ToString() => $"[{Type}: {Start}..{End}]";
}