using Verifex.Analysis.Symbols;
using Verifex.CodeGen;
using Verifex.Parsing;

namespace Verifex.Analysis.Verification.Pass;

public class NameCheckPass(SymbolTable symbolTable) : VerificationPass(symbolTable)
{
    protected override void Visit(VarDeclNode node)
    {
        if (SymbolTable.GetLocal(node.Name) != null)
            ErrorAt(node, $"'{node.Name}' already declared in this scope");
        else
            SymbolTable.AddLocalToCurrentScope(node.Name, node.Type, LocationType.Local);
        
        if (node.TypeHint != null && SymbolTable.GetType(node.TypeHint) == null)
            ErrorAt(node, $"unknown type '{node.TypeHint}'");
    }

    protected override void Visit(FunctionCallNode node)
    {
        if (node.Callee is not IdentifierNode identifier) return;
        
        if (SymbolTable.GetFunction(identifier.Identifier) == null)
            ErrorAt(node, $"unknown function '{identifier.Identifier}'");
    }

    protected override void Visit(FunctionDeclNode node)
    {
        if (node.ReturnType != null && SymbolTable.GetType(node.ReturnType) == null)
            ErrorAt(node, $"unknown type '{node.ReturnType}'");
        
        SymbolTable.PushScope(); // for the function parameters
        foreach (TypedIdentifierNode parameter in node.Parameters)
        {
            if (SymbolTable.GetLocal(parameter.Identifier) != null)
                ErrorAt(parameter, $"parameter '{parameter.Identifier}' already declared");
            
            if (SymbolTable.GetType(parameter.TypeName) == null)
                ErrorAt(parameter, $"unknown type '{parameter.TypeName}'");
            
            SymbolTable.AddLocalToCurrentScope(parameter.Identifier, SymbolTable.GetType(parameter.TypeName), LocationType.Parameter);
        }
        
        base.Visit(node);
        SymbolTable.PopScope();
    }

    protected override void Visit(IdentifierNode node)
    {
        if (!SymbolTable.HasSymbol(node.Identifier))
            ErrorAt(node, $"unknown identifier '{node.Identifier}'");
    }
}