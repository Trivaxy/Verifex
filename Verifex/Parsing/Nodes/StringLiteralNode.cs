namespace Verifex.Parsing.Nodes;

public class StringLiteralNode(string value) : AstNode
{
    public readonly string Value = value;
}