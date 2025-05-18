using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

public class SecondBindingPass(VerificationContext context) : VerificationPass(context)
{
    protected override void Visit(InitializerNode node)
    {
        if (!Symbols.TryLookupGlobalSymbol(node.Type.Identifier, out TypeSymbol? typeSymbol))
        {
            LogDiagnostic(new UnknownType(node.Type.Identifier) { Location = node.Location });
            return;
        }

        node.Type.Symbol = typeSymbol;

        if (typeSymbol is not StructSymbol structSymbol)
            return; // this is an error, but the type annotation pass will catch it later
        
        foreach (InitializerFieldNode field in node.InitializerList.Values)
        {
            Visit(field.Value);

            if (!structSymbol.Fields.TryGetValue(field.Name.Identifier, out StructFieldSymbol? fieldSymbol))
            {
                LogDiagnostic(new UnknownStructField(structSymbol.Name, field.Name.Identifier) { Location = field.Location });
                continue;
            }
            
            field.Name.Symbol = fieldSymbol;
        }
    }
}
