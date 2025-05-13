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
            node.ResolvedType = (left is IntegerType && right is IntegerType) ? Symbols.GetType("Int") : Symbols.GetType("Real");

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

        if (node.Callee is not IdentifierNode functionName || !Symbols.TryLookupSymbol(functionName.Identifier, out FunctionSymbol? functionSymbol))
        {
            LogDiagnostic(new InvalidFunctionCall() { Location = node.Callee.Location });
            return;
        }

        node.ResolvedType = functionSymbol!.Function.ReturnType;
    }

    protected override void Visit(VarDeclNode node)
    {
        base.Visit(node);

        if (node.TypeHint != null)
        {
            if (Symbols.TryLookupGlobalSymbol(node.TypeHint, out TypeSymbol? typeSymbol))
                node.Symbol!.ResolvedType = typeSymbol!.ResolvedType;
            else
                LogDiagnostic(new UnknownType(node.TypeHint) { Location = node.Location });
        }
        else
            node.Symbol!.ResolvedType = node.Value.ResolvedType;
    }

    protected override void Visit(ParamDeclNode node)
    {
        base.Visit(node);
        
        if (!Symbols.TryLookupGlobalSymbol(node.TypeName, out TypeSymbol? typeSymbol))
        {
            LogDiagnostic(new UnknownType(node.TypeName) { Location = node.Location });
            return;
        }
        
        node.Symbol!.ResolvedType = typeSymbol!.ResolvedType;
    }

    protected override void Visit(StructFieldNode node)
    {
        base.Visit(node);
        
        if (!Symbols.TryLookupGlobalSymbol(node.Type, out TypeSymbol? typeSymbol))
            LogDiagnostic(new UnknownType(node.Type) { Location = node.Location });
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
        Visit(node.Target);
        
        // don't report those errors, the binding pass catches them
        if (node.Target.ResolvedType?.EffectiveType is not StructType structType) return;
        if (!structType.Fields.TryGetValue(node.Member.Identifier, out FieldInfo? field)) return;

        node.ResolvedType = field.Type;
    }
}
