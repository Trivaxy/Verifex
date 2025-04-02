using System.Collections.ObjectModel;
using Verifex.Analysis.Symbols;
using Verifex.Parsing;

namespace Verifex.Analysis.Verification.Pass;

public abstract class VerificationPass(SymbolTable symbolTable) : DefaultNodeVisitor
{
    private readonly List<CompileDiagnostic> _diagnostics = [];
    protected readonly SymbolTable SymbolTable = symbolTable;

    public ReadOnlyCollection<CompileDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    protected override void Visit(BlockNode node)
    {
        SymbolTable.PushScope();
        base.Visit(node);
        SymbolTable.PopScope();
    }

    protected void ErrorAt(AstNode node, string message) =>
        _diagnostics.Add(new CompileDiagnostic(node.Location, message, DiagnosticLevel.Error));
    
    protected void WarningAt(AstNode node, string message) =>
        _diagnostics.Add(new CompileDiagnostic(node.Location, message, DiagnosticLevel.Warning));

    public static VerificationPass[] CreateRegularPasses(SymbolTable symbolTable) =>
    [
        new FunctionGatheringPass(symbolTable),
        new TypeAnnotatorPass(symbolTable),
        new NameCheckPass(symbolTable),
    ];
}