using Verifex.Parsing.Nodes;

namespace Verifex.Analysis;

public class DefaultNodeVisitor : NodeVisitor
{
    public override void Visit(ProgramNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public override void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
    }

    public override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public override void Visit(FunctionCallNode node)
    {
        Visit(node.Callee);
        foreach (AstNode argument in node.Arguments)
            Visit(argument);
    }

    public override void Visit(FunctionDeclNode node)
    {
        foreach (TypedIdentifierNode parameter in node.Parameters)
            Visit(parameter);
        Visit(node.Body);
    }

    public override void Visit(IdentifierNode node) {}

    public override void Visit(NumberNode node) {}

    public override void Visit(TypedIdentifierNode node) {}

    public override void Visit(UnaryNegationNode node) => Visit(node.Operand);

    public override void Visit(VarDeclNode node) => Visit(node.Value);

    public override void Visit(StringLiteralNode node) {}

    public override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
    }
}