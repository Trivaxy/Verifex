using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Checks for type mismatches throughout the annotated AST
public class TypeMismatchPass(SymbolTable symbols) : VerificationPass(symbols)
{
    private VerifexFunction _currentFunction = null!;
    
    protected override void Visit(FunctionDeclNode node)
    {
        _currentFunction = (node.Symbol as FunctionSymbol)!.Function;
        base.Visit(node);
    }

    // Checks that binary operations are performed on compatible types that support arithmetic
    protected override void Visit(BinaryOperationNode node)
    {
        base.Visit(node);

        VerifexType leftType = node.Left.ResolvedType!;
        VerifexType rightType = node.Right.ResolvedType!;
        
        switch (TypeSupportsArithmetic(leftType), TypeSupportsArithmetic(rightType))
        {
            case (true, true):
                if (leftType != rightType)
                    LogDiagnostic(new BinaryOpTypeMismatch(node.Operator.ToString(), leftType.Name, rightType.Name) { Location = node.Location });
                break;
            case (false, true):
                LogDiagnostic(new TypeCannotDoArithmetic(leftType.Name) { Location = node.Location });
                break;
            case (true, false):
                LogDiagnostic(new TypeCannotDoArithmetic(rightType.Name) { Location = node.Location });
                break;
            case (false, false):
                LogDiagnostic(new TypeCannotDoArithmetic(leftType.Name) { Location = node.Location });
                LogDiagnostic(new TypeCannotDoArithmetic(rightType.Name) { Location = node.Location });
                break;
        }
    }

    protected override void Visit(UnaryNegationNode node)
    {
        base.Visit(node);
        
        if (!TypeSupportsArithmetic(node.Operand.ResolvedType!))
            LogDiagnostic(new TypeCannotDoArithmetic(node.Operand.ResolvedType!.Name) { Location = node.Location });
    }

    protected override void Visit(VarDeclNode node)
    {
        base.Visit(node);
        
        if (node.TypeHint != null && Symbols.TryLookupGlobalSymbol(node.TypeHint, out BuiltinTypeSymbol? typeSymbol))
        {
            if (node.Value.ResolvedType != typeSymbol!.ResolvedType)
                LogDiagnostic(new VarDeclTypeMismatch(node.Name, typeSymbol.ResolvedType!.Name, node.Value.ResolvedType!.Name) { Location = node.Location });
        }
    }

    protected override void Visit(ReturnNode node)
    {
        base.Visit(node);
        
        if (node.Value == null)
            return;
        
        if (node.Value.ResolvedType != _currentFunction.ReturnType)
            LogDiagnostic(new ReturnTypeMismatch(_currentFunction.Name, _currentFunction.ReturnType.Name, node.Value.ResolvedType!.Name) { Location = node.Location });
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
        
        for (int i = 0; i < Math.Min(function.Parameters.Count, node.Arguments.Count); i++)
        {
            ParameterInfo parameter = function.Parameters[i];
            VerifexType expectedType = parameter.Type;
            VerifexType actualType = node.Arguments[i].ResolvedType!;
            
            if (expectedType != actualType)
                LogDiagnostic(new ParamTypeMismatch(parameter.Name, expectedType.Name, actualType.Name) { Location = node.Arguments[i].Location });
        }
    }

    private static bool TypeSupportsArithmetic(VerifexType type) => type.Name is "Int" or "Real";
}