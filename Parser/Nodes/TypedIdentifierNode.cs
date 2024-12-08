namespace Verifex.Parser.Nodes;

public class TypedIdentifierNode(string name, string type) : IdentifierNode(name)
{
    public readonly String TypeName = type;
}