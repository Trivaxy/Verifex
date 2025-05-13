using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Validates immutability constraints in the program
public class MutationCheckPass(SymbolTable symbols) : VerificationPass(symbols)
{
    // Check that assignment targets are mutable variables
    protected override void Visit(AssignmentNode node)
    {
        base.Visit(node);

        AstNode target = node.Target;
        while (target is MemberAccessNode memberAccess)
            target = memberAccess.Target;
        
        if (target.Symbol is not LocalVarSymbol localSymbol) return;
        
        if (!localSymbol.IsMutable)
            LogDiagnostic(new ImmutableVarReassignment(localSymbol.Name) { Location = node.Target.Location });
    }

    protected override void Visit(MemberAccessNode node) => Visit(node.Target);
}