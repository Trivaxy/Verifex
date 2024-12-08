namespace Verifex.Parser.Nodes;

public class NumberNode(int value) : AstNode
{
    public readonly int Value = value;
}