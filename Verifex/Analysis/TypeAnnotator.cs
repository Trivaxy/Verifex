using Verifex.Analysis.Symbols;
using Verifex.CodeGen;
using Verifex.Parsing.Nodes;

namespace Verifex.Analysis;

public class TypeAnnotator(SymbolTable symbolTable) : NodeVisitor
{
    public override void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
        
        // By default, the type of the binary operation is the same as the left operand
        node.Type = node.Left.Type;
    }

    public override void Visit(FunctionCallNode node)
    {
        Visit(node.Callee);
        
        // Check if the callee is a function
        if (node.Callee is IdentifierNode identifier)
        {
            VerifexFunction? function = symbolTable.GetFunction(identifier.Identifier);
            if (function != null)
                node.Type = function.ReturnType;
        }
        else
            throw new InvalidOperationException("Callee is not an identifier");
        
        foreach (AstNode argument in node.Arguments)
            Visit(argument);
    }

    public override void Visit(IdentifierNode node) => node.Type = symbolTable.GetType(node.Identifier);

    public override void Visit(NumberNode node) =>
        node.Type = node.NumberType == NumberType.Integer ? symbolTable.Integer : symbolTable.Real;

    public override void Visit(TypedIdentifierNode node) => node.Type = symbolTable.GetType(node.TypeName);

    public override void Visit(UnaryNegationNode node)
    {
        Visit(node.Operand);
        node.Type = node.Operand.Type;
    }

    public override void Visit(VarDeclNode node)
    {
        Visit(node.Value);
        node.Type = node.Value.Type;
    }

    public override void Visit(StringLiteralNode node) => node.Type = symbolTable.String;

    public override void Visit(ReturnNode node)
    {
        if (node.Value == null) return; 
        
        Visit(node.Value);
        node.Type = node.Value.Type;
    }
    
    public override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public override void Visit(FunctionDeclNode node) => Visit(node.Body);

    public override void Visit(ProgramNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }
}