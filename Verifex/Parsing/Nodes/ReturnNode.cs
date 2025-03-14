namespace Verifex.Parsing.Nodes;

public class ReturnNode(AstNode? value) : AstNode
{
    public ReturnNode() : this(null) { }
    
    public readonly AstNode? Value = value;
}