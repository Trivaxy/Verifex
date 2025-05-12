using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Attach type info to nodes representing primitive type literals
public class PrimitiveTypeAnnotationPass(SymbolTable symbols) : VerificationPass(symbols)
{
    protected override void Visit(NumberNode node) => node.ResolvedType = Symbols.GetType(node.NumberType == NumberType.Integer ? "Int" : "Real");

    protected override void Visit(StringLiteralNode node) => node.ResolvedType = Symbols.GetType("String");

    protected override void Visit(BoolLiteralNode node) => node.ResolvedType = Symbols.GetType("Bool");
}