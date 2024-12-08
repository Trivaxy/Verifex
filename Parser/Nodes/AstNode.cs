namespace Verifex.Parser.Nodes;

public abstract class AstNode
{
    private readonly int _sourceStart;
    private readonly int _sourceLength;

    public virtual IEnumerable<AstNode> VisitChildren() => [];
}