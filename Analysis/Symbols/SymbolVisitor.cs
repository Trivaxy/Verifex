using Verifex.Parser.Nodes;

namespace Verifex.Analysis.Symbols;

public class SymbolVisitor : NodeVisitor
{
    private Symbols _symbols = new Symbols();
    
    public override void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
    }

    public override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public override void Visit(FunctionCallNode node) {}

    public override void Visit(FunctionDeclNode node)
    {
        throw new NotImplementedException();
    }

    public override void Visit(IdentifierNode node)
    {
        throw new NotImplementedException();
    }

    public override void Visit(NumberNode node)
    {
        throw new NotImplementedException();
    }

    public override void Visit(TypedIdentifierNode node)
    {
        throw new NotImplementedException();
    }

    public override void Visit(UnaryNegationNode node)
    {
        throw new NotImplementedException();
    }

    public override void Visit(VarDeclNode node)
    {
        throw new NotImplementedException();
    }
}