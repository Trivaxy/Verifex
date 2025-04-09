using System.Collections.ObjectModel;
using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public abstract class VerificationPass(SymbolTable symbols) : DefaultNodeVisitor
{
    private readonly List<CompileDiagnostic> _diagnostics = [];
    protected readonly SymbolTable Symbols = symbols;
    
    public ReadOnlyCollection<CompileDiagnostic> Diagnostics => _diagnostics.AsReadOnly();

    protected void LogDiagnostic(CompileDiagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public static VerificationPass[] CreateRegularPasses(out SymbolTable symbols)
    {
        symbols = SymbolTable.CreateDefaultTable();

        return
        [
            new TopLevelGatheringPass(symbols),
            new BindingPass(symbols),
            new PrimitiveTypeAnnotationPass(symbols),
            new TypeAnnotationPass(symbols),
            new TypeMismatchPass(symbols),
            new MutationCheckPass(symbols),
        ];
    }
}