namespace Verifex.Parser.Nodes;

public class UnaryNegationNode(AstNode operand) : AstNode
{
    public readonly AstNode Operand = operand;
}