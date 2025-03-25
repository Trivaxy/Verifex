using Verifex.Analysis.Symbols;
using Verifex.CodeGen;
using Verifex.Parsing.Nodes;

namespace Verifex.Analysis.Verification.Pass;

public class FunctionGatheringPass(SymbolTable symbolTable) : VerificationPass(symbolTable)
{
    protected override void Visit(FunctionDeclNode node)
    {
        if (SymbolTable.GetFunction(node.Name) != null)
        {
            ErrorAt(node, $"function '{node.Name}' already declared");
            return;
        }

        VerifexFunction function = new VerifexFunction(
            node.Name,
            node.Parameters
                .Select(typedIdent => new ParameterInfo(typedIdent.Identifier, SymbolTable.GetType(typedIdent.TypeName)))
                .ToList()
                .AsReadOnly(),
            node.ReturnType != null ? SymbolTable.GetType(node.ReturnType) : SymbolTable.GetType("Void"));
        
        SymbolTable.AddFunction(function);
    }
}
