using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public class CfgConstructionPass(VerificationContext context) : VerificationPass(context)
{
    protected override void Visit(FunctionDeclNode node)
    {
        FunctionSymbol function = (node.Symbol as FunctionSymbol)!;
        if (Context.ControlFlowGraphs.ContainsKey(function)) return; // check needed because functions with the same name point to same symbol
        
        Context.ControlFlowGraphs[function] = CFGBuilder.Build(node);
    }
}