using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Attach type info to nodes representing primitive type literals, as well as attempt to resolve type nodes which are obvious
public class StaticAnnotationPass(VerificationContext context) : VerificationPass(context)
{
    protected override void Visit(NumberNode node) => node.ResolvedType = Symbols.GetType(node.NumberType == NumberType.Integer ? "Int" : "Real")!;

    protected override void Visit(StringLiteralNode node) => node.ResolvedType = Symbols.GetType("String")!;

    protected override void Visit(BoolLiteralNode node) => node.ResolvedType = Symbols.GetType("Bool")!;

    protected override void Visit(SimpleTypeNode node)
    {
        string typeName = node.Identifier;
            
        if (!Symbols.TryLookupGlobalSymbol(typeName, out TypeSymbol? typeSymbol))
            LogDiagnostic(new UnknownType(typeName) { Location = node.Location });

        node.Symbol = typeSymbol;
    }
    
    protected override void Visit(MaybeTypeNode node)
    {
        base.Visit(node);
        node.ResolvedType = new MaybeType(node.Types.Select(t => t.ResolvedType).ToList().AsReadOnly()!);
    }
    
    protected override void Visit(ArrayTypeNode node)
    {
        base.Visit(node);
        node.ResolvedType = new ArrayType(node.ElementType.EffectiveType);
    }
}