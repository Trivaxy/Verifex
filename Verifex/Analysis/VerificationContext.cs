using Verifex.CodeGen;

namespace Verifex.Analysis;

public class VerificationContext(SymbolTable symbols)
{
    public readonly SymbolTable Symbols = symbols;
    
    public readonly List<CompileDiagnostic> Diagnostics = [];

    public readonly Dictionary<FunctionSymbol, ControlFlowGraph> ControlFlowGraphs = [];
}