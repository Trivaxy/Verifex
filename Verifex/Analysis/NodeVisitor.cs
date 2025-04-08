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
    protected abstract void Visit(ParamDeclNode node);
    protected abstract void Visit(MinusNegationNode node);
    protected abstract void Visit(VarDeclNode node);
    protected abstract void Visit(StringLiteralNode node);
    protected abstract void Visit(ReturnNode node);
    protected abstract void Visit(BoolLiteralNode node);
    protected abstract void Visit(NotNegationNode node);
    protected abstract void Visit(IfElseNode node);
    
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
            case ParamDeclNode typedIdentifierNode:
                Visit(typedIdentifierNode);
                break;
            case IdentifierNode identifierNode:
                Visit(identifierNode);
                break;
            case NumberNode numberNode:
                Visit(numberNode);
                break;
            case MinusNegationNode unaryNegationNode:
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
            case BoolLiteralNode boolLiteralNode:
                Visit(boolLiteralNode);
                break;
            case NotNegationNode notNegationNode:
                Visit(notNegationNode);
                break;
            case IfElseNode ifElseNode:
                Visit(ifElseNode);
                break;
        }
    }
}
