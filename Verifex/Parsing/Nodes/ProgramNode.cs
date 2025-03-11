using System.Collections.ObjectModel;

namespace Verifex.Parsing.Nodes;

public class ProgramNode(ReadOnlyCollection<AstNode> nodes) : AstNode
{
    public readonly ReadOnlyCollection<AstNode> Nodes = nodes;
}