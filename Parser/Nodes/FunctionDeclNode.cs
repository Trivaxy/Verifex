using System.Collections.ObjectModel;

namespace Verifex.Parser.Nodes;

public class FunctionDeclNode(string name, ReadOnlyCollection<TypedIdentifierNode> parameters, string? returnType, BlockNode body) : AstNode
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<TypedIdentifierNode> Parameters = parameters;
    public readonly string? ReturnType = returnType;
    public readonly BlockNode Body = body;
}