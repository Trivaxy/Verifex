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
    
    protected override void Visit(StructDeclNode node)
    {
        Dictionary<string, StructFieldSymbol> fields = [];
        foreach (StructFieldNode fieldNode in node.Fields)
        {
            if (fields.ContainsKey(fieldNode.Name))
            {
                LogDiagnostic(new DuplicateField(fieldNode.Name) { Location = fieldNode.Location });
                continue;
            }
            
            fieldNode.Symbol = new StructFieldSymbol()
            {
                DeclaringNode = fieldNode,
                Name = fieldNode.Name,
                ResolvedType = VerifexType.Delayed(() => Symbols.GetType(fieldNode.Type)),
            };
            
            fields.Add(fieldNode.Symbol.Name, (fieldNode.Symbol as StructFieldSymbol)!);
        }

        StructSymbol structSymbol = new StructSymbol()
        {
            DeclaringNode = node,
            Name = node.Name,
            ResolvedType = new StructType(node.Name,
                node.Fields.Select(f => new FieldInfo(f.Name, VerifexType.Delayed(() => Symbols.GetType(f.Type))))
                    .ToDictionary(f => f.Name).AsReadOnly()),
            Fields = fields.AsReadOnly(),
        };

        if (!Symbols.TryAddGlobalSymbol(structSymbol))
            LogDiagnostic(new DuplicateTopLevelSymbol(structSymbol.Name) { Location = node.Location });
        else
            node.Symbol = structSymbol;
    }
}