using Verifex.Parsing;

namespace Verifex.Analysis;

public abstract class NodeVisitor
{
    protected abstract void Visit(ProgramNode node);
    protected abstract void Visit(BinaryOperationNode node);
    protected abstract void Visit(BlockNode node);
    protected abstract void Visit(FunctionCallNode node);
    protected abstract void Visit(FunctionDeclNode node);
    protected abstract void Visit(IdentifierNode node);
    protected abstract void Visit(NumberNode node);
    protected abstract void Visit(TypedIdentifierNode node);
    protected abstract void Visit(UnaryNegationNode node);
    protected abstract void Visit(VarDeclNode node);
    protected abstract void Visit(StringLiteralNode node);
    protected abstract void Visit(ReturnNode node);
    
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
