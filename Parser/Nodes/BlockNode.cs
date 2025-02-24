using System.Collections.ObjectModel;

namespace Verifex.Parser.Nodes;

public class BlockNode(ReadOnlyCollection<AstNode> nodes) : AstNode
{
    public readonly ReadOnlyCollection<AstNode> Nodes = nodes;
}