using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Attach type info to nodes representing primitive type literals
public class PrimitiveTypeAnnotationPass(SymbolTable symbols) : VerificationPass(symbols)
{
    protected override void Visit(NumberNode node)
    {
        node.ResolvedType = Symbols.GetSymbol<Symbol>(node.NumberType == NumberType.Integer ? "Int" : "Real").ResolvedType;
    }

    protected override void Visit(StringLiteralNode node)
    {
        node.ResolvedType = Symbols.GetSymbol<Symbol>("String").ResolvedType;
    }
    
    protected override void Visit(BoolLiteralNode node)
    {
        node.ResolvedType = Symbols.GetSymbol<Symbol>("Bool").ResolvedType;
    }
}