namespace Verifex.Parsing;

public readonly record struct Token(TokenType Type, Range Range)
{
    public override string ToString() => Type.ToSimpleString();
}