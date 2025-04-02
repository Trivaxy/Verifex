using Verifex.Analysis.Symbols;
using Verifex.Analysis.Verification.Pass;
using Verifex.CodeGen;
using Verifex.Parsing;

namespace Verifex.Analysis.Verification.Pass;

public class TypeAnnotatorPass(SymbolTable symbolTable) : VerificationPass(symbolTable)
{
    protected override void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
        
        // By default, the type of the binary operation is the same as the left operand
        node.Type = node.Left.Type;
    }

    protected override void Visit(FunctionCallNode node)
    {
        Visit(node.Callee);
        
        // Check if the callee is a function
        if (node.Callee is IdentifierNode identifier)
        {
            VerifexFunction? function = SymbolTable.GetFunction(identifier.Identifier);
            if (function != null)
                node.Type = function.ReturnType;
        }
        else
            throw new InvalidOperationException("Callee is not an identifier");
        
        foreach (AstNode argument in node.Arguments)
            Visit(argument);
    }

    protected override void Visit(IdentifierNode node) => node.Type = SymbolTable.GetType(node.Identifier);

    protected override void Visit(NumberNode node) =>
        node.Type = node.NumberType == NumberType.Integer ? SymbolTable.Integer : SymbolTable.Real;

    protected override void Visit(TypedIdentifierNode node) => node.Type = SymbolTable.GetType(node.TypeName);

    protected override void Visit(UnaryNegationNode node)
    {
        Visit(node.Operand);
        node.Type = node.Operand.Type;
    }

    protected override void Visit(VarDeclNode node)
    {
        Visit(node.Value);
        node.Type = node.Value.Type;
    }

    protected override void Visit(StringLiteralNode node) => node.Type = SymbolTable.String;

    protected override void Visit(ReturnNode node)
    {
        if (node.Value == null) return; 
        
        Visit(node.Value);
        node.Type = node.Value.Type;
    }
    
    protected override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    protected override void Visit(FunctionDeclNode node) => Visit(node.Body);

    protected override void Visit(ProgramNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }
}
