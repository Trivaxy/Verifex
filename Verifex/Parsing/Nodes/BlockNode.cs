using System.Collections.ObjectModel;

namespace Verifex.Parsing.Nodes;

public class BlockNode(ReadOnlyCollection<AstNode> nodes) : AstNode
{
    public readonly ReadOnlyCollection<AstNode> Nodes = nodes;
}