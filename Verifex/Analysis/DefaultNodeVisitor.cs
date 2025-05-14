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
        foreach (ParamDeclNode parameter in node.Parameters)
            Visit(parameter);
        Visit(node.Body);
    }

    protected override void Visit(IdentifierNode node) {}

    protected override void Visit(NumberNode node) {}

    protected override void Visit(ParamDeclNode node) {}

    protected override void Visit(MinusNegationNode node) => Visit(node.Operand);

    protected override void Visit(VarDeclNode node) => Visit(node.Value);

    protected override void Visit(StringLiteralNode node) {}

    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
    }

    protected override void Visit(BoolLiteralNode node) {}

    protected override void Visit(NotNegationNode node) => Visit(node.Operand);

    protected override void Visit(IfElseNode node)
    {
        Visit(node.Condition);
        Visit(node.IfBody);
        if (node.ElseBody != null)
            Visit(node.ElseBody);
    }

    protected override void Visit(AssignmentNode node)
    {
        Visit(node.Target);
        Visit(node.Value);
    }

    protected override void Visit(WhileNode node)
    {
        Visit(node.Condition);
        Visit(node.Body);
    }

    protected override void Visit(RefinedTypeDeclNode node)
    {
        Visit(node.Expression);
    }

    protected override void Visit(StructDeclNode node)
    {
        foreach (StructFieldNode field in node.Fields)
            Visit(field);
        
        foreach (StructMethodNode method in node.Methods)
            Visit(method);
    }
    
    protected override void Visit(StructFieldNode node) {}

    protected override void Visit(MemberAccessNode node)
    {
        Visit(node.Target);
        // don't visit the member
    }
    
    protected override void Visit(InitializerNode node)
    {
        Visit(node.Type);
        Visit(node.InitializerList);
    }
    
    protected override void Visit(InitializerListNode node)
    {
        foreach (InitializerFieldNode field in node.Values)
            Visit(field);
    }
    
    protected override void Visit(InitializerFieldNode node)
    {
        Visit(node.Name);
        Visit(node.Value);
    }

    protected override void Visit(StructMethodNode node) => Visit(node.Function);
}
