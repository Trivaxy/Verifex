using Verifex.CodeGen;
using Verifex.Parser.Nodes;

namespace Verifex.Analysis.Symbols;

public class SymbolGatherer : NodeVisitor
{
    private readonly Symbols _symbols = new Symbols();
    
    public override void Visit(FunctionDeclNode node)
    {
        VerifexFunction function = new VerifexFunction(
            node.Name,
            node.Parameters
                .Select(typedIdent => new ParameterInfo(typedIdent.Identifier, _symbols.GetType(typedIdent.TypeName)))
                .ToList()
                .AsReadOnly(),
            node.ReturnType != null ? _symbols.GetType(node.ReturnType) : _symbols.GetType("Void"));
        
        _symbols.AddFunction(function);
    }

    public static Symbols Gather(ProgramNode program)
    {
        SymbolGatherer gatherer = new SymbolGatherer();
        gatherer.Visit(program);
        
        return gatherer._symbols;
    }
}