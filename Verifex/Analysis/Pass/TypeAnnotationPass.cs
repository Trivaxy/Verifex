using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Attach type info to expression nodes, and give symbols types
public class TypeAnnotationPass(SymbolTable symbols) : VerificationPass(symbols)
{
    protected override void Visit(BinaryOperationNode node)
    {
        base.Visit(node);
        
        if (node.Left.ResolvedType == null || node.Right.ResolvedType == null)
            return;

        if (node.Operator.Type.IsBoolOp() && node.Left.FundamentalType is BoolType && node.Right.FundamentalType is BoolType)
        {
            node.ResolvedType = Symbols.GetType("Bool");
            return;
        }
        
        if (node.Operator.Type is TokenType.EqualEqual or TokenType.NotEqual && node.Left.FundamentalType == node.Right.FundamentalType)
        {
            node.ResolvedType = Symbols.GetType("Bool");
            return;
        }
        
        if (node.Operator.Type.IsComparisonOp()
            && node.Left.FundamentalType is IntegerType or RealType
            && node.Right.FundamentalType is IntegerType or RealType)
        {
            node.ResolvedType = Symbols.GetType("Bool");
            return;
        }

        VerifexType left = node.Left.ResolvedType;
        VerifexType right = node.Right.ResolvedType;
        
        // string concat
        if (node.Operator.Type == TokenType.Plus)
        {
            if (left.FundamentalType is StringType || right.FundamentalType is StringType)
            {
                node.ResolvedType = Symbols.GetType("String");
                return;
            }
        }
        
        if (node.Operator.Type.IsArithmeticOp()
            && left.FundamentalType is IntegerType or RealType
            && right.FundamentalType is IntegerType or RealType)
            node.ResolvedType = (left.FundamentalType is IntegerType && right.FundamentalType is IntegerType) ? Symbols.GetType("Int") : Symbols.GetType("Real");

        // if none of the above, we have a type mismatch so ResolvedType stays null
    }

    protected override void Visit(MinusNegationNode node)
    {
        base.Visit(node);
        node.ResolvedType = node.Operand.ResolvedType;
    }

    protected override void Visit(NotNegationNode node)
    {
        base.Visit(node);
        node.ResolvedType = Symbols.GetType("Bool");
    }
    
    protected override void Visit(FunctionCallNode node)
    {
        base.Visit(node);

        if (node.Callee.Symbol is FunctionSymbol functionSymbol)
            node.ResolvedType = functionSymbol.Function.ReturnType;
    }

    protected override void Visit(VarDeclNode node)
    {
        base.Visit(node);

        if (node.TypeHint != null)
        {
            if (node.TypeHint.EffectiveType == null)
                LogDiagnostic(new UnknownType(node.TypeHint.ToString()) { Location = node.Location });
            else
                node.Symbol!.ResolvedType = node.TypeHint.EffectiveType;
        }
        else
            node.Symbol!.ResolvedType = node.Value.ResolvedType;
    }

    protected override void Visit(ParamDeclNode node)
    {
        base.Visit(node);
        
        if (node.Type.EffectiveType == null)
        {
            LogDiagnostic(new UnknownType(node.Type.ToString()) { Location = node.Location });
            return;
        }
        
        node.Symbol!.ResolvedType = node.Type.ResolvedType;
    }

    protected override void Visit(StructFieldNode node)
    {
        base.Visit(node);
        
        if (node.Type.EffectiveType == null)
            LogDiagnostic(new UnknownType(node.Type.ToString()) { Location = node.Location });
    }

    protected override void Visit(InitializerNode node)
    {
        if (!Symbols.TryLookupGlobalSymbol(node.Type.Identifier, out TypeSymbol? typeSymbol))
        {
            LogDiagnostic(new UnknownType(node.Type.Identifier) { Location = node.Location });
            return;
        }
        
        if (typeSymbol is not StructSymbol structSymbol)
        {
            LogDiagnostic(new TypeCannotHaveInitializer(node.Type.Identifier) { Location = node.Location });
            return;
        }

        node.ResolvedType = structSymbol.ResolvedType;
        Visit(node.InitializerList);
    }
    
    protected override void Visit(InitializerFieldNode node)
    {
        Visit(node.Value);
    }

    protected override void Visit(MemberAccessNode node)
    {
        base.Visit(node);
        
        if (node.Target.Symbol is FunctionSymbol func)
        {
            if (!func.Function.IsStatic)
            {
                string structName = node.Target.EffectiveType?.Name ?? "unknown";
                LogDiagnostic(new MemberAccessOnNonStruct(structName, node.Member.Identifier) { Location = node.Location });
            }
        }
        else if (node.Target.Symbol is StructSymbol structSymbol)
        {
            bool isStaticAccess = node.Target is IdentifierNode identifier && identifier.Identifier == structSymbol.Name;

            if (!isStaticAccess)
            {
                if (structSymbol.Fields.TryGetValue(node.Member.Identifier, out StructFieldSymbol? fieldSymbol))
                    node.Symbol = fieldSymbol;
                else if (structSymbol.Methods.TryGetValue(node.Member.Identifier, out FunctionSymbol? methodSymbol))
                    node.Symbol = methodSymbol;
                else
                    LogDiagnostic(new UnknownStructField(structSymbol.Name, node.Member.Identifier) { Location = node.Location });
            }
        }
        else if (node.Target.FundamentalType is StructType structType)
        {
            structSymbol = Symbols.GetSymbol<StructSymbol>(structType.Name);
            if (structSymbol.Fields.TryGetValue(node.Member.Identifier, out StructFieldSymbol? fieldSymbol))
                node.Symbol = fieldSymbol;
            else if (structSymbol.Methods.TryGetValue(node.Member.Identifier, out FunctionSymbol? methodSymbol))
                node.Symbol = methodSymbol;
            else
                LogDiagnostic(new UnknownStructField(structSymbol.Name, node.Member.Identifier) { Location = node.Location });
        }
    }

    protected override void Visit(MaybeTypeNode node)
    {
        node.ResolvedType = new MaybeType(node.Types.Select(t => t.ResolvedType).ToList().AsReadOnly()!);
    }

    protected override void Visit(IsCheckNode node)
    {
        node.ResolvedType = Symbols.GetType("Bool");
    }
}
