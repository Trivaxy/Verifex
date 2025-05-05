using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Gathering pass for top-level symbols, like functions, structs, etc.
public class TopLevelGatheringPass(SymbolTable symbols) : VerificationPass(symbols)
{
    protected override void Visit(FunctionDeclNode node)
    {
        FunctionSymbol function = new FunctionSymbol()
        {
            DeclaringNode = node,
            Name = node.Name,
            Function = new VerifexFunction(node.Name, node.Parameters.Select(p =>
            {
                Visit(p);
                return new ParameterInfo(p.Identifier, VerifexType.Delayed(() => Symbols.GetType(p.TypeName)));
            }).ToList(), VerifexType.Delayed(() => Symbols.GetType(node.ReturnType ?? "Void")))
        };

        if (!Symbols.TryAddGlobalSymbol(function))
            LogDiagnostic(new DuplicateTopLevelSymbol(function.Name) { Location = node.Location });
        else
            node.Symbol = function;
    }

    protected override void Visit(RefinedTypeDeclNode node)
    {
        VerifexType baseType = VerifexType.Delayed(() => Symbols.GetType(node.BaseType));
        RefinedTypeSymbol refined = new RefinedTypeSymbol()
        {
            DeclaringNode = node,
            Name = node.Name,
            ResolvedType = new RefinedType(node.Name, baseType, node.Expression),
            BaseType = baseType,
        };
        
        if (!Symbols.TryAddGlobalSymbol(refined))
            LogDiagnostic(new DuplicateTopLevelSymbol(refined.Name) { Location = node.Location });
        else
            node.Symbol = refined;
    }
}