namespace Verifex.Parsing.Nodes;

public class NumberNode(string value) : AstNode
{
    public readonly string Value = value;
    public readonly NumberType NumberType = value.Contains('.') ? NumberType.Real : NumberType.Integer;

    public int AsInteger() => int.Parse(Value);

    public double AsDouble() => double.Parse(Value);
}

public enum NumberType
{
    Integer,
    Real
}
