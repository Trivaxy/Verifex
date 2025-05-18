using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Checks that all paths in functions that have return values do in fact return
public class ReturnFlowPass(VerificationContext context) : VerificationPass(context)
{
    protected override void Visit(FunctionDeclNode node)
    {
        FunctionSymbol function = (node.Symbol as FunctionSymbol)!;
        
        if (function.Function.ReturnType == null || function.Function.ReturnType == VerifexType.Unknown)
            return;

        ControlFlowGraph cfg = Context.ControlFlowGraphs[function];
        BasicBlock exit = cfg.Exit;

        foreach (BasicBlock exitPredecessor in exit.Predecessors)
        {
            if (exitPredecessor.Statements.Count == 0 || exitPredecessor.Statements[^1] is not ReturnNode)
            {
                LogDiagnostic(new NotAllPathsReturn(function.Name) { Location = node.ReturnType!.Location });
                return;
            }
        }
    }
}