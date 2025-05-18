using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public class CfgConstructionPass(VerificationContext context) : VerificationPass(context)
{
    protected override void Visit(FunctionDeclNode node)
    {
        ControlFlowGraph cfg = CFGBuilder.Build(node);
        Context.ControlFlowGraphs[(node.Symbol as FunctionSymbol)!] = cfg;
    }
}