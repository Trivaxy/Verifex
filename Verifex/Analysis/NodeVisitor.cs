using Verifex.Parsing.Nodes;

namespace Verifex.Analysis;

public abstract class NodeVisitor
{
    public abstract void Visit(ProgramNode node);
    public abstract void Visit(BinaryOperationNode node);
    public abstract void Visit(BlockNode node);
    public abstract void Visit(FunctionCallNode node);
    public abstract void Visit(FunctionDeclNode node);
    public abstract void Visit(IdentifierNode node);
    public abstract void Visit(NumberNode node);
    public abstract void Visit(TypedIdentifierNode node);
    public abstract void Visit(UnaryNegationNode node);
    public abstract void Visit(VarDeclNode node);
    public abstract void Visit(StringLiteralNode node);
    public abstract void Visit(ReturnNode node);
    
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
            case StringLiteralNode stringLiteralNode:
                Visit(stringLiteralNode);
                break;
            case ReturnNode returnNode:
                Visit(returnNode);
                break;
        }
    }
}