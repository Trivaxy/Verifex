using System.Collections.ObjectModel;

namespace Verifex.Parser.Nodes;

public class BlockNode(ReadOnlyCollection<AstNode> nodes)
{
    public readonly ReadOnlyCollection<AstNode> Nodes = nodes;
}