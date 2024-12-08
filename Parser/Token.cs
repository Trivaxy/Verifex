namespace Verifex.Parser;

public readonly struct Token(TokenType type, ReadOnlyMemory<char> text)
{
    public readonly TokenType Type = type;
    public readonly ReadOnlyMemory<char> Text = text;

    public override string ToString() => $"[{Type}: {Text}]";
}