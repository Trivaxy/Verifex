using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public abstract class VerificationPass(VerificationContext context) : DefaultNodeVisitor
{
    protected readonly VerificationContext Context = context;

    protected SymbolTable Symbols => Context.Symbols;
    
    public void Run(AstNode node)
    {
        Visit(node);
        PostPass();
    }
    
    protected virtual void PostPass() {}

    protected void LogDiagnostic(CompileDiagnostic diagnostic) => Context.Diagnostics.Add(diagnostic);

    public static VerificationPass[] CreateRegularPasses(out VerificationContext context)
    { 
        context = new VerificationContext(SymbolTable.CreateDefaultTable());

        return
        [
            new TopLevelGatheringPass(context),
            new CfgConstructionPass(context),
            new FirstBindingPass(context),
            new PrimitiveTypeAnnotationPass(context),
            new TypeAnnotationPass(context),
            new SecondBindingPass(context),
            new BasicTypeMismatchPass(context),
            new RefinedTypeMismatchPass(context),
            new MutationCheckPass(context),
            new ReturnFlowPass(context),
        ];
    }
}