namespace Verifex.Parsing.Nodes;

public class BinaryOperationNode(Token operatorToken, AstNode left, AstNode right) : AstNode
{
    public readonly Token Operator = operatorToken;
    public readonly AstNode Left = left;
    public readonly AstNode Right = right;
}