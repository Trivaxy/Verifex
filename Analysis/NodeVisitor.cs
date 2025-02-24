using Verifex.Parser.Nodes;

namespace Verifex.Analysis;

public abstract class NodeVisitor
{
    public virtual void Visit(BinaryOperationNode node) {}

    public virtual void Visit(BlockNode node) {}

    public virtual void Visit(FunctionCallNode node) {}

    public virtual void Visit(FunctionDeclNode node) {}

    public virtual void Visit(IdentifierNode node) {}
    
    public virtual void Visit(NumberNode node) {}

    public virtual void Visit(TypedIdentifierNode node) {}

    public virtual void Visit(UnaryNegationNode node) => Visit(node.Operand);

    public virtual void Visit(VarDeclNode node) {}
    
    public void Visit(AstNode node)
    {
        switch (node)
        {
            case BinaryOperationNode binaryOperationNode:
                Visit(binaryOperationNode);
                break;
            case BlockNode blockNode:
                Visit(blockNode);
                break;
            case FunctionCallNode functionCallNode:
                Visit(functionCallNode);
                break;
            case FunctionDeclNode functionDeclNode:
                Visit(functionDeclNode);
                break;
            case TypedIdentifierNode typedIdentifierNode:
                Visit(typedIdentifierNode);
                break;
            case IdentifierNode identifierNode:
                Visit(identifierNode);
                break;
            case NumberNode numberNode:
                Visit(numberNode);
                break;
            case UnaryNegationNode unaryNegationNode:
                Visit(unaryNegationNode);
                break;
            case VarDeclNode varDeclNode:
                Visit(varDeclNode);
                break;
        }
    }
}