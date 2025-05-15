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
            }).ToList(), VerifexType.Delayed(() => Symbols.GetType(node.ReturnType ?? "Void")), null, node.IsStatic)
        };
        
        if (node.IsStatic)
            LogDiagnostic(new StaticFunctionOutsideStruct() { Location = node.Location });
        
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
        Dictionary<string, FunctionSymbol> methods = [];

        int i = 0;
        foreach (StructFieldNode fieldNode in node.Fields)
        {
            if (fields.ContainsKey(fieldNode.Name))
            {
                LogDiagnostic(new DuplicateMember(fieldNode.Name) { Location = fieldNode.Location });
                continue;
            }
            
            fieldNode.Symbol = new StructFieldSymbol()
            {
                DeclaringNode = fieldNode,
                Name = fieldNode.Name,
                ResolvedType = VerifexType.Delayed(() => Symbols.GetType(fieldNode.Type)),
                Owner = null!, // set below
                Index = i,
            };
            
            fields.Add(fieldNode.Symbol.Name, (fieldNode.Symbol as StructFieldSymbol)!);
            i++;
        }
        
        foreach (StructMethodNode methodNode in node.Methods)
        {
            if (fields.ContainsKey(methodNode.Function.Name) || methods.ContainsKey(methodNode.Function.Name))
            {
                LogDiagnostic(new DuplicateMember(methodNode.Function.Name) { Location = methodNode.Location });
                continue;
            }
            
            methodNode.Symbol = new FunctionSymbol()
            {
                DeclaringNode = methodNode,
                Name = methodNode.Function.Name,
                Function = new VerifexFunction(
                    methodNode.Function.Name,
                    methodNode.Function.Parameters.Select(p => 
                    {
                        Visit(p);
                        return new ParameterInfo(p.Identifier, VerifexType.Delayed(() => Symbols.GetType(p.TypeName)));
                    }).ToList(),
                    VerifexType.Delayed(() => Symbols.GetType(methodNode.Function.ReturnType ?? "Void")),
                    VerifexType.Delayed(() => Symbols.GetType(node.Name)),
                    methodNode.Function.IsStatic),
            };
            methodNode.Function.Symbol = methodNode.Symbol;
            
            methods.Add(methodNode.Symbol.Name, (methodNode.Symbol as FunctionSymbol)!);
        }

        StructSymbol structSymbol = new StructSymbol()
        {
            DeclaringNode = node,
            Name = node.Name,
            ResolvedType = new StructType(node.Name,
                node.Fields.Select(f => new FieldInfo(f.Name, VerifexType.Delayed(() => Symbols.GetType(f.Type))))
                    .ToDictionary(f => f.Name)),
            Fields = fields,
            Methods = methods,
        };
        
        foreach (StructFieldSymbol field in structSymbol.Fields.Values)
            field.Owner = structSymbol;

        if (!Symbols.TryAddGlobalSymbol(structSymbol))
            LogDiagnostic(new DuplicateTopLevelSymbol(structSymbol.Name) { Location = node.Location });
        else
            node.Symbol = structSymbol;
    }
    
    protected override void Visit(StructMethodNode node) {} // no-op
}