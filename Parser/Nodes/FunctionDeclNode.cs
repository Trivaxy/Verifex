using System.Collections.ObjectModel;

namespace Verifex.Parser.Nodes;

public class FunctionDeclNode(string name, ReadOnlyCollection<TypedIdentifierNode> parameters, BlockNode body)
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<TypedIdentifierNode> Parameters = parameters;
    public readonly BlockNode Body = body;
}