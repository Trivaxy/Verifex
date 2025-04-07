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
        symbols = CreateDefaultSymbolTable();

        return
        [
            new TopLevelGatheringPass(symbols),
            new BindingPass(symbols),
            new PrimitiveTypeAnnotationPass(symbols),
            new TypeAnnotationPass(symbols),
            new TypeMismatchPass(symbols),
        ];
    }

    private static SymbolTable CreateDefaultSymbolTable()
    {
        SymbolTable symbols = new();
        symbols.TryAddGlobalSymbol(BuiltinTypeSymbol.Create(new IntegerType()));
        symbols.TryAddGlobalSymbol(BuiltinTypeSymbol.Create(new RealType()));
        symbols.TryAddGlobalSymbol(BuiltinTypeSymbol.Create(new VoidType()));
        symbols.TryAddGlobalSymbol(BuiltinTypeSymbol.Create(new StringType()));

        symbols.TryAddGlobalSymbol(BuiltinFunctionSymbol.Create(new BuiltinFunction("print",
            [new ParameterInfo("value", symbols.GetSymbol<Symbol>("Int").ResolvedType!)],
            symbols.GetSymbol<Symbol>("Void").ResolvedType!,
            typeof(Console).GetMethod("WriteLine", [typeof(int)])!)));
        
        return symbols;
    }
}