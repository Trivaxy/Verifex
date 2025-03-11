namespace Verifex.Parsing.Nodes;

public class UnaryNegationNode(AstNode operand) : AstNode
{
    public readonly AstNode Operand = operand;
}