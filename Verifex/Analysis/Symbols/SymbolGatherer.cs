using Verifex.CodeGen;
using Verifex.Parsing.Nodes;

namespace Verifex.Analysis.Symbols;

public class SymbolGatherer : DefaultNodeVisitor
{
    private readonly SymbolTable _symbolTable = new SymbolTable();
    
    protected override void Visit(FunctionDeclNode node)
    {
        VerifexFunction function = new VerifexFunction(
            node.Name,
            node.Parameters
                .Select(typedIdent => new ParameterInfo(typedIdent.Identifier, _symbolTable.GetType(typedIdent.TypeName)))
                .ToList()
                .AsReadOnly(),
            node.ReturnType != null ? _symbolTable.GetType(node.ReturnType) : _symbolTable.GetType("Void"));
        
        _symbolTable.AddFunction(function);
    }

    public static SymbolTable Gather(ProgramNode program)
    {
        SymbolGatherer gatherer = new SymbolGatherer();
        gatherer.Visit(program);
        
        return gatherer._symbolTable;
    }
}
