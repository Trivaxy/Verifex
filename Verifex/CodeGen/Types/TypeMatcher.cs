using Microsoft.Z3;

namespace Verifex.CodeGen.Types;

public class TypeMatcher
{
    public static bool IsTypeAssignable(VerifexType from, VerifexType to, IEnumerable<BoolExpr>? constraints = null)
    {
        if (from is VoidType || to is VoidType) return false;
        if (from == to) return true;
        if (from.IlType != to.IlType) return false;
        
        using var ctx = new Context();
        using var solver = ctx.MkSolver();
        
        if (constraints != null)
            solver.Assert(constraints.ToArray());

        throw new NotImplementedException();
    }
}