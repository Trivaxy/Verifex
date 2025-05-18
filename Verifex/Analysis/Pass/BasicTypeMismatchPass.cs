using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Checks for basic type mismatches throughout the annotated AST
public class BasicTypeMismatchPass(VerificationContext context) : VerificationPass(context)
{
    // Checks that binary operations are performed on compatible types
    protected override void Visit(BinaryOperationNode node)
    {
        base.Visit(node);

        VerifexType leftType = node.Left.ResolvedType;
        VerifexType rightType = node.Right.ResolvedType;

        if (leftType == VerifexType.Unknown || rightType == VerifexType.Unknown)
            return; // don't bother if there's no types, means something failed earlier

        if (node.Operator.Type.IsBoolOp())
        {
            if (leftType.FundamentalType is not BoolType)
                LogDiagnostic(new TypeCannotDoBoolOps(leftType.Name) { Location = node.Left.Location });
            
            if (rightType.FundamentalType is not BoolType)
                LogDiagnostic(new TypeCannotDoBoolOps(rightType.Name) { Location = node.Right.Location });
        }
        else if (node.Operator.Type.IsComparisonOp())
        {
            if (node.Operator.Type is TokenType.EqualEqual or TokenType.NotEqual)
            {
                if (leftType.FundamentalType != rightType.FundamentalType)
                    LogDiagnostic(new BinaryOpTypeMismatch(node.Operator.Type.ToSimpleString(), leftType.Name, rightType.Name) { Location = node.Location });
            }
            else
            {
                if (!TypeSupportsArithmetic(leftType))
                    LogDiagnostic(new TypeCannotDoComparison(leftType.Name) { Location = node.Left.Location });
            
                if (!TypeSupportsArithmetic(rightType))
                    LogDiagnostic(new TypeCannotDoComparison(rightType.Name) { Location = node.Right.Location });
            }
        }
        else if (node.Operator.Type.IsArithmeticOp())
        {
            if (node.Operator.Type == TokenType.Plus && (leftType.FundamentalType is StringType || rightType.FundamentalType is StringType))
                return;
            
            switch (TypeSupportsArithmetic(leftType), TypeSupportsArithmetic(rightType))
            {
                case (false, true):
                    LogDiagnostic(new TypeCannotDoArithmetic(leftType.Name) { Location = node.Left.Location });
                    break;
                case (true, false):
                    LogDiagnostic(new TypeCannotDoArithmetic(rightType.Name) { Location = node.Right.Location });
                    break;
                case (false, false):
                    LogDiagnostic(new TypeCannotDoArithmetic(leftType.Name) { Location = node.Left.Location });
                    LogDiagnostic(new TypeCannotDoArithmetic(rightType.Name) { Location = node.Right.Location });
                    break;
            }
        }
    }

    protected override void Visit(MinusNegationNode node)
    {
        base.Visit(node);
        
        if (node.Operand.ResolvedType == VerifexType.Unknown)
            return;
        
        if (!TypeSupportsArithmetic(node.Operand.ResolvedType))
            LogDiagnostic(new TypeCannotDoArithmetic(node.Operand.ResolvedType.Name) { Location = node.Location });
    }
    
    protected override void Visit(FunctionCallNode node)
    {
        base.Visit(node);

        if (node.Callee.Symbol is not FunctionSymbol functionSymbol) return;

        VerifexFunction function = functionSymbol.Function;
        
        if (function.Parameters.Count < node.Arguments.Count)
            LogDiagnostic(new TooManyArguments(function.Name, function.Parameters.Count, node.Arguments.Count) { Location = node.Location });
        else if (function.Parameters.Count > node.Arguments.Count)
            LogDiagnostic(new NotEnoughArguments(function.Name, function.Parameters.Count, node.Arguments.Count) { Location = node.Location });
    }

    protected override void Visit(IfElseNode node)
    {
        base.Visit(node);
        
        if (node.Condition.ResolvedType == VerifexType.Unknown)
            return;
            
        if (node.Condition.ResolvedType.Name != "Bool")
            LogDiagnostic(new ConditionMustBeBool("if") { Location = node.Condition.Location });
    }

    protected override void Visit(WhileNode node)
    {
        base.Visit(node);

        if (node.Condition.ResolvedType == VerifexType.Unknown)
            return;
            
        if (node.Condition.ResolvedType.Name != "Bool")
            LogDiagnostic(new ConditionMustBeBool("while") { Location = node.Condition.Location });
    }

    protected override void Visit(IsCheckNode node)
    {
        if (!SupportsIsCheck(node.Value.ResolvedType))
            LogDiagnostic(new IsCheckOnNonMaybeType() { Location = node.Location });
    }

    protected override void Visit(InitializerNode node)
    {
        if (node.Type.Symbol is not StructSymbol structSymbol)
            return; // error caught earlier
        
        HashSet<string> seen = [];
        foreach (InitializerFieldNode field in node.InitializerList.Values)
            seen.Add(field.Name.Identifier);
        
        foreach (StructFieldSymbol field in structSymbol.Fields.Values)
        {
            if (!seen.Contains(field.Name))
                LogDiagnostic(new StructFieldNotInitialized(node.Type.Identifier, field.Name) { Location = node.Location });
        }
    }

    private static bool SupportsIsCheck(VerifexType type)
    {
        if (type.EffectiveType is MaybeType) return true;
        if (type.EffectiveType is RefinedType refined) return SupportsIsCheck(refined.BaseType);
        return false;
    }

    private static bool TypeSupportsArithmetic(VerifexType type) => type.FundamentalType is IntegerType or RealType;
}