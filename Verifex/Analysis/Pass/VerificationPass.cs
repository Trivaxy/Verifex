using System.Collections.ObjectModel;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public abstract class VerificationPass(SymbolTable symbols) : DefaultNodeVisitor
{
    private readonly List<CompileDiagnostic> _diagnostics = [];
    protected readonly SymbolTable Symbols = symbols;
    
    public ReadOnlyCollection<CompileDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    public void Run(AstNode node)
    {
        Visit(node);
        PostPass();
    }
    
    protected virtual void PostPass() {}

    protected void LogDiagnostic(CompileDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public static VerificationPass[] CreateRegularPasses(out SymbolTable symbols)
    {
        symbols = SymbolTable.CreateDefaultTable();

        return
        [
            new TopLevelGatheringPass(symbols),
            new FirstBindingPass(symbols),
            new PrimitiveTypeAnnotationPass(symbols),
            new TypeAnnotationPass(symbols),
            new SecondBindingPass(symbols),
            new BasicTypeMismatchPass(symbols),
            new RefinedTypeMismatchPass(symbols),
            new MutationCheckPass(symbols),
        ];
    }
}