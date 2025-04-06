using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Attach type info to nodes representing primitive type literals
public class PrimitiveTypeAnnotationPass(SymbolTable symbols) : VerificationPass(symbols)
{
    protected override void Visit(NumberNode node)
    {
        if (!Symbols.TryLookupSymbol(node.NumberType == NumberType.Integer ? "Int" : "Real", out Symbol? symbol))
            throw new Exception("Failed to find numeric type symbol");

        node.ResolvedType = symbol.ResolvedType;
    }

    protected override void Visit(StringLiteralNode node)
    {
        if (!Symbols.TryLookupSymbol("String", out Symbol? symbol))
            throw new Exception("Failed to find string type symbol");

        node.ResolvedType = symbol.ResolvedType;
    }
}