namespace Verifex.Parser.Nodes;

public class IdentifierNode(string identifier) : AstNode
{
    public readonly string Identifier = identifier;
}