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
        
        if (node.ReturnType != null)
            Visit(node.ReturnType);
        
        Visit(node.Body);
    }

    protected override void Visit(IdentifierNode node) {}

    protected override void Visit(NumberNode node) {}

    protected override void Visit(ParamDeclNode node) => Visit(node.Type);

    protected override void Visit(MinusNegationNode node) => Visit(node.Operand);

    protected override void Visit(VarDeclNode node)
    {
        if (node.TypeHint != null)
            Visit(node.TypeHint);
        Visit(node.Value);
    }

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
        Visit(node.BaseType);
        Visit(node.Expression);
    }

    protected override void Visit(StructDeclNode node)
    {
        foreach (StructFieldNode field in node.Fields)
            Visit(field);
        
        foreach (StructMethodNode method in node.Methods)
            Visit(method);
    }

    protected override void Visit(StructFieldNode node) => Visit(node.Type);

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

    protected override void Visit(SimpleTypeNode node) {}

    protected override void Visit(MaybeTypeNode node)
    {
        foreach (AstNode type in node.Types)
            Visit(type);
    }

    protected override void Visit(IsCheckNode node)
    {
        Visit(node.Value);
        Visit(node.TestedType);
    }
    
    protected override void Visit(ArrayLiteralNode node)
    {
        foreach (AstNode element in node.Elements)
            Visit(element);
    }

    protected override void Visit(ArrayTypeNode node) => Visit(node.ElementType);
    
    protected override void Visit(IndexAccessNode node)
    {
        Visit(node.Target);
        Visit(node.Index);
    }

    protected override void Visit(GetLengthNode node) => Visit(node.Target);

    protected override void Visit(FunctionSignatureNode node)
    {
        foreach (ParamDeclNode param in node.Parameters)
            Visit(param);
        
        if (node.ReturnType != null)
            Visit(node.ReturnType);
    }

    protected override void Visit(ArchetypeDeclNode node)
    {
        foreach (FunctionSignatureNode method in node.Methods)
            Visit(node);
        
        foreach (StructFieldNode field in node.Fields)
            Visit(field);
    }
}
