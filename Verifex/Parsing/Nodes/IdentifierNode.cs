namespace Verifex.Parsing.Nodes;

public class IdentifierNode(string identifier) : AstNode
{
    public readonly string Identifier = identifier;
}