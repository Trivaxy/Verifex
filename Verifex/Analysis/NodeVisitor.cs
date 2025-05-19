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
    protected abstract void Visit(AssignmentNode node);
    protected abstract void Visit(WhileNode node);
    protected abstract void Visit(RefinedTypeDeclNode node);
    protected abstract void Visit(StructDeclNode node);
    protected abstract void Visit(StructFieldNode node);
    protected abstract void Visit(MemberAccessNode node);
    protected abstract void Visit(InitializerNode node);
    protected abstract void Visit(InitializerListNode node);
    protected abstract void Visit(InitializerFieldNode node);
    protected abstract void Visit(StructMethodNode node);
    protected abstract void Visit(SimpleTypeNode node);
    protected abstract void Visit(MaybeTypeNode node);
    protected abstract void Visit(IsCheckNode node);
    protected abstract void Visit(ArrayLiteralNode node);
    protected abstract void Visit(ArrayTypeNode node);
    protected abstract void Visit(IndexAccessNode node);
    protected abstract void Visit(GetLengthNode node);
    
    protected void Visit(AstNode node)
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
            case SimpleTypeNode simpleTypeNode:
                Visit(simpleTypeNode);
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
            case AssignmentNode assignmentNode:
                Visit(assignmentNode);
                break;
            case WhileNode whileNode:
                Visit(whileNode);
                break;
            case RefinedTypeDeclNode refinedTypeDeclNode:
                Visit(refinedTypeDeclNode);
                break;
            case StructDeclNode structDeclNode:
                Visit(structDeclNode);
                break;
            case StructFieldNode structFieldNode:
                Visit(structFieldNode);
                break;
            case MemberAccessNode memberAccessNode:
                Visit(memberAccessNode);
                break;
            case InitializerNode initializerNode:
                Visit(initializerNode);
                break;
            case InitializerListNode initializerListNode:
                Visit(initializerListNode);
                break;
            case InitializerFieldNode initializerFieldNode:
                Visit(initializerFieldNode);
                break;
            case StructMethodNode structMethodNode:
                Visit(structMethodNode);
                break;
            case MaybeTypeNode maybeTypeNode:
                Visit(maybeTypeNode);
                break;
            case IsCheckNode isCheckNode:
                Visit(isCheckNode);
                break;
            case ArrayLiteralNode arrayLiteralNode:
                Visit(arrayLiteralNode);
                break;
            case ArrayTypeNode arrayTypeNode:
                Visit(arrayTypeNode);
                break;
            case IndexAccessNode indexAccessNode:
                Visit(indexAccessNode);
                break;
            case GetLengthNode getLengthNode:
                Visit(getLengthNode);
                break;
        }
    }
}
