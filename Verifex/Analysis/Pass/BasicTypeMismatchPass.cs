using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Checks for basic type mismatches throughout the annotated AST
public class BasicTypeMismatchPass(SymbolTable symbols) : VerificationPass(symbols)
{
    // Checks that binary operations are performed on compatible types
    protected override void Visit(BinaryOperationNode node)
    {
        base.Visit(node);

        VerifexType? leftType = node.Left.ResolvedType;
        VerifexType? rightType = node.Right.ResolvedType;

        if (leftType == null || rightType == null)
            return; // don't bother if there's no types, means something failed earlier

        if (node.Operator.Type.IsBoolOp())
        {
            if (leftType.Name != "Bool")
                LogDiagnostic(new TypeCannotDoBoolOps(leftType.Name) { Location = node.Left.Location });
            
            if (rightType.Name != "Bool")
                LogDiagnostic(new TypeCannotDoBoolOps(rightType.Name) { Location = node.Right.Location });
        }
        else if (node.Operator.Type.IsComparisonOp())
        {
            if (node.Operator.Type is TokenType.EqualEqual or TokenType.NotEqual)
            {
                if (leftType != rightType)
                    LogDiagnostic(new BinaryOpTypeMismatch(node.Operator.ToString(), leftType.Name, rightType.Name) { Location = node.Location });
            }
            else
            {
                if (leftType.Name != "Int" && leftType.Name != "Real")
                    LogDiagnostic(new TypeCannotDoComparison(leftType.Name) { Location = node.Left.Location });
            
                if (rightType.Name != "Int" && rightType.Name != "Real")
                    LogDiagnostic(new TypeCannotDoComparison(rightType.Name) { Location = node.Right.Location });
            }
        }
        else
        {
            switch (TypeSupportsArithmetic(leftType), TypeSupportsArithmetic(rightType))
            {
                case (true, true):
                    if (leftType != rightType)
                        LogDiagnostic(new BinaryOpTypeMismatch(node.Operator.ToString(), leftType.Name, rightType.Name) { Location = node.Location });
                    break;
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
        
        if (node.Operand.ResolvedType == null)
            return;
        
        if (!TypeSupportsArithmetic(node.Operand.ResolvedType))
            LogDiagnostic(new TypeCannotDoArithmetic(node.Operand.ResolvedType.Name) { Location = node.Location });
    }

    protected override void Visit(VarDeclNode node)
    {
        base.Visit(node);
        
        if (node.TypeHint != null && Symbols.TryLookupGlobalSymbol(node.TypeHint, out TypeSymbol? typeSymbol))
        {
            if (node.Value.ResolvedType != typeSymbol!.ResolvedType)
                LogDiagnostic(new VarDeclTypeMismatch(node.Name, typeSymbol.ResolvedType!.Name, (node.Value.ResolvedType != null ? node.Value.ResolvedType.Name : "unknown")) { Location = node.Location });
        }
    }
    
    protected override void Visit(FunctionCallNode node)
    {
        base.Visit(node);
        
        if (node.Callee is not IdentifierNode identifierNode)
            throw new InvalidOperationException("Function call must be an identifier");

        if (!Symbols.TryLookupGlobalSymbol(identifierNode.Identifier, out FunctionSymbol? functionSymbol))
            return;

        VerifexFunction function = functionSymbol!.Function;
        
        if (function.Parameters.Count < node.Arguments.Count)
            LogDiagnostic(new TooManyArguments(function.Name, function.Parameters.Count, node.Arguments.Count) { Location = node.Location });
        else if (function.Parameters.Count > node.Arguments.Count)
            LogDiagnostic(new NotEnoughArguments(function.Name, function.Parameters.Count, node.Arguments.Count) { Location = node.Location });
    }

    protected override void Visit(IfElseNode node)
    {
        base.Visit(node);
        
        if (node.Condition.ResolvedType == null)
            return;
            
        if (node.Condition.ResolvedType.Name != "Bool")
            LogDiagnostic(new ConditionMustBeBool("if") { Location = node.Condition.Location });
    }

    protected override void Visit(WhileNode node)
    {
        base.Visit(node);

        if (node.Condition.ResolvedType == null)
            return;
            
        if (node.Condition.ResolvedType.Name != "Bool")
            LogDiagnostic(new ConditionMustBeBool("while") { Location = node.Condition.Location });
    }

    private static bool TypeSupportsArithmetic(VerifexType type) => type.Name is "Int" or "Real";
}