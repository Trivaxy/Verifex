using Microsoft.Z3;
using Verifex.Parsing;

namespace Verifex.Analysis;

public class Z3Mapper(Context ctx, Dictionary<Symbol, Z3Expr> termMap)
{
    public Z3Expr ConvertExpr(AstNode node)
    {
        switch (node)
        {
            case NumberNode n: return ConvertNumber(n);
            case IdentifierNode id: return ConvertIdentifier(id);
            case BinaryOperationNode binOp: return ConvertBinaryOperation(binOp);
            case MinusNegationNode unOp: return ConvertMinusNegation(unOp);
            // add cases for other expression types: StringLiteralNode, BooleanLiteralNode?, Function Calls (if simple enough), etc.
            default: 
                throw new NotImplementedException($"Z3 conversion not implemented for AST node type: {node.GetType().Name}");
        }
    }

    private Z3Expr ConvertNumber(NumberNode node)
    {
        if (node.NumberType == NumberType.Integer)
            return ctx.MkInt(node.AsInteger());

        return ctx.MkReal(node.AsDouble().ToString());
    }

    private Z3Expr ConvertIdentifier(IdentifierNode node)
    {
        if (node.Symbol == null)
            throw new InvalidOperationException($"Identifier '{node.Identifier}' has no associated symbol");

        if (termMap.TryGetValue(node.Symbol, out Z3Expr? z3Expr))
            return z3Expr;

        throw new InvalidOperationException($"No Z3 expression found for symbol '{node.Symbol.Name}' in the current context");
    }

    private Z3Expr ConvertBinaryOperation(BinaryOperationNode node)
    {
        Z3Expr left = ConvertExpr(node.Left);
        Z3Expr right = ConvertExpr(node.Right);

        switch (node.Operator.Type)
        {
            case TokenType.Plus: return ctx.MkAdd((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.Minus: return ctx.MkSub((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.Star: return ctx.MkMul((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.Slash: return ctx.MkDiv((Z3ArithExpr)left, (Z3ArithExpr)right);

            case TokenType.Equals: return ctx.MkEq(left, right); // Generic equality
            case TokenType.NotEqual: return ctx.MkNot(ctx.MkEq(left, right));
            case TokenType.LessThan: return ctx.MkLt((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.LessThanOrEqual: return ctx.MkLe((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.GreaterThan: return ctx.MkGt((Z3ArithExpr)left, (Z3ArithExpr)right);
            case TokenType.GreaterThanOrEqual: return ctx.MkGe((Z3ArithExpr)left, (Z3ArithExpr)right);

            case TokenType.And: return ctx.MkAnd((Z3BoolExpr)left, (Z3BoolExpr)right);
            case TokenType.Or: return ctx.MkOr((Z3BoolExpr)left, (Z3BoolExpr)right);

            default:
                throw new NotImplementedException($"Z3 conversion not implemented for binary operator: {node.Operator.Type}");
        }
    }

    private Z3Expr ConvertMinusNegation(MinusNegationNode node)
    {
        Z3Expr operand = ConvertExpr(node.Operand);
        return ctx.MkUnaryMinus((Z3ArithExpr)operand);
    }
}