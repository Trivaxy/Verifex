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

        if (node.Operator.Type.IsBoolOp() && node.Left.ResolvedType.EffectiveType is BoolType && node.Right.ResolvedType.EffectiveType is BoolType)
        {
            node.ResolvedType = Symbols.GetType("Bool");
            return;
        }
        
        if (node.Operator.Type is TokenType.EqualEqual or TokenType.NotEqual && node.Left.ResolvedType == node.Right.ResolvedType)
        {
            node.ResolvedType = Symbols.GetType("Bool");
            return;
        }
        
        if (node.Operator.Type.IsComparisonOp()
            && node.Left.ResolvedType.EffectiveType is IntegerType or RealType && node.Right.ResolvedType.EffectiveType is IntegerType or RealType)
        {
            node.ResolvedType = Symbols.GetType("Bool");
            return;
        }

        VerifexType left = node.Left.ResolvedType.EffectiveType;
        VerifexType right = node.Right.ResolvedType.EffectiveType;
        
        // if the types are not the same, but they are both a subtype of the same base type, use the right base type
        if (left != right && left.IlType == right.IlType)
        {
            Type baseIlType = left.IlType;
            if (baseIlType == typeof(int))
                node.ResolvedType = Symbols.GetType("Int");
            else if (baseIlType == typeof(double))
                node.ResolvedType = Symbols.GetType("Real");
        }
        else if (node.Left.ResolvedType.EffectiveType == node.Right.ResolvedType.EffectiveType)
            node.ResolvedType = node.Left.ResolvedType;
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
}
