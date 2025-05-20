using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public abstract class VerificationPass(VerificationContext context) : DefaultNodeVisitor
{
    protected readonly VerificationContext Context = context;

    protected SymbolTable Symbols => Context.Symbols;
    
    public void Run(AstNode node) => Visit(node);

    protected void LogDiagnostic(CompileDiagnostic diagnostic) => Context.Diagnostics.Add(diagnostic);

    public static VerificationPass[] CreateRegularPasses(out VerificationContext context)
    { 
        context = new VerificationContext(SymbolTable.CreateDefaultTable());

        return
        [
            new TopLevelGatheringPass(context),
            new CfgConstructionPass(context),
            new FirstBindingPass(context),
            new StaticAnnotationPass(context),
            new SubsequentAnnotationPass(context),
            new SecondBindingPass(context),
            new RefiningPass(context),
            new BasicTypeMismatchPass(context),
            new MutationCheckPass(context),
            new ReturnFlowPass(context),
        ];
    }
}