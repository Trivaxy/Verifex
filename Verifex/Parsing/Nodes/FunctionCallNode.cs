using System.Collections.ObjectModel;

namespace Verifex.Parsing.Nodes;

public class FunctionCallNode(AstNode callee, ReadOnlyCollection<AstNode> arguments) : AstNode
{
    public readonly AstNode Callee = callee;
    public readonly ReadOnlyCollection<AstNode> Arguments = arguments;
}