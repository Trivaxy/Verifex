using Verifex.Parsing;

namespace Verifex.Analysis;

public class DefaultNodeVisitor : NodeVisitor
{
    protected override void Visit(ProgramNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    protected override void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
    }

    protected override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    protected override void Visit(FunctionCallNode node)
    {
        Visit(node.Callee);
        foreach (AstNode argument in node.Arguments)
            Visit(argument);
    }

    protected override void Visit(FunctionDeclNode node)
    {
        foreach (TypedIdentifierNode parameter in node.Parameters)
            Visit(parameter);
        Visit(node.Body);
    }

    protected override void Visit(IdentifierNode node) {}

    protected override void Visit(NumberNode node) {}

    protected override void Visit(TypedIdentifierNode node) {}

    protected override void Visit(UnaryNegationNode node) => Visit(node.Operand);

    protected override void Visit(VarDeclNode node) => Visit(node.Value);

    protected override void Visit(StringLiteralNode node) {}

    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
    }
}
