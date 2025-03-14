using Verifex.Parsing.Nodes;

namespace Verifex.Analysis;

public abstract class NodeVisitor
{
    public virtual void Visit(ProgramNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public virtual void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
    }

    public virtual void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public virtual void Visit(FunctionCallNode node)
    {
        Visit(node.Callee);
        foreach (AstNode argument in node.Arguments)
            Visit(argument);
    }

    public virtual void Visit(FunctionDeclNode node)
    {
        foreach (TypedIdentifierNode parameter in node.Parameters)
            Visit(parameter);
        Visit(node.Body);
    }

    public virtual void Visit(IdentifierNode node) {}
    
    public virtual void Visit(NumberNode node) {}

    public virtual void Visit(TypedIdentifierNode node) {}

    public virtual void Visit(UnaryNegationNode node) => Visit(node.Operand);

    public virtual void Visit(VarDeclNode node) => Visit(node.Value);
    
    public void Visit(AstNode node)
    {
        switch (node)
        {
            case ProgramNode program:
                Visit(program);
                break;
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