using Microsoft.Z3;
using Verifex.Analysis;
using Verifex.Analysis.Mapping;
using Verifex.Analysis.Pass;
using Verifex.Parsing;
using Symbol = Verifex.Analysis.Symbol;

namespace Verifex.Tests;

public class Z3MapperTests
{
    private readonly Context _ctx = new();

    private Z3Mapper CreateMapper(Dictionary<Symbol, Z3Expr>? map = null) => new Z3Mapper(_ctx, _ctx.MkSolver(), new TermStack(), SymbolTable.CreateDefaultTable());

    private static NumberNode IntNode(int value) => new NumberNode(value.ToString());
    
    private static NumberNode RealNode(double value) => new NumberNode(value.ToString());
    
    private static IdentifierNode Ident(string name, Symbol? symbol = null) => new IdentifierNode(name) { Symbol = symbol };
    
    private static BinaryOperationNode BinOp(TokenType op, AstNode left, AstNode right) => new BinaryOperationNode(new Token(op, 0..1), left, right);

    [Fact]
    public void ConvertNumber_IntegerNode_ReturnsZ3Int()
    {
        var node = IntNode(42);
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<IntNum>(expr);
        Assert.Equal(42, ((IntNum)expr).Int);
    }

    [Fact]
    public void ConvertNumber_RealNode_ReturnsZ3Real()
    {
        var node = RealNode(3.14);
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<RatNum>(expr);
        Assert.Equal("157/50", ((RatNum)expr).ToString());
    }

    [Fact]
    public void ConvertIdentifier_MapsToZ3Expr()
    {
        var symbol = new LocalVarSymbol { Name = "x", DeclaringNode = null, IsMutable = false, IsParameter = false, Index = 0 };
        var node = Ident("x", symbol);
        var z3Var = _ctx.MkIntConst("x");
        var expr = CreateMapper(new() { { symbol, z3Var } }).ConvertExpr(node);
        
        Assert.Equal(z3Var, expr);
    }

    [Fact]
    public void ConvertBinaryOperation_Addition_ReturnsZ3Add()
    {
        var node = BinOp(TokenType.Plus, IntNode(2), IntNode(3));
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3IntExpr>(expr);
        Assert.Equal("(+ 2 3)", expr.ToString());
    }

    [Fact]
    public void ConvertBinaryOperation_Equality_ReturnsZ3Eq()
    {
        var node = BinOp(TokenType.Equals, IntNode(2), IntNode(2));
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3BoolExpr>(expr);
        Assert.Equal("(= 2 2)", expr.ToString());
    }

    [Fact]
    public void ConvertMinusNegationNode_ReturnsZ3UnaryMinus()
    {
        var node = new MinusNegationNode(IntNode(5));
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3IntExpr>(expr);
        Assert.Equal("(- 5)", expr.ToString());
    }

    [Fact]
    public void ConvertBinaryOperation_LogicalAnd_ReturnsZ3And()
    {
        var left = BinOp(TokenType.Equals, IntNode(1), IntNode(1));
        var right = BinOp(TokenType.Equals, IntNode(2), IntNode(2));
        var node = BinOp(TokenType.And, left, right);
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3BoolExpr>(expr);
        Assert.Equal("(and (= 1 1) (= 2 2))", expr.ToString());
    }

    [Fact]
    public void ConvertBinaryOperation_LogicalOr_ReturnsZ3Or()
    {
        var left = BinOp(TokenType.Equals, IntNode(1), IntNode(2));
        var right = BinOp(TokenType.Equals, IntNode(3), IntNode(3));
        var node = BinOp(TokenType.Or, left, right);
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3BoolExpr>(expr);
        Assert.Equal("(or (= 1 2) (= 3 3))", expr.ToString());
    }

    [Fact]
    public void ConvertBinaryOperation_NotEqual_ReturnsZ3NotEq()
    {
        var node = BinOp(TokenType.NotEqual, IntNode(1), IntNode(2));
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3BoolExpr>(expr);
        Assert.Equal("(not (= 1 2))", expr.ToString());
    }

    [Fact]
    public void ConvertBinaryOperation_LessThan_ReturnsZ3Lt()
    {
        var node = BinOp(TokenType.LessThan, IntNode(1), IntNode(2));
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3BoolExpr>(expr);
        Assert.Equal("(< 1 2)", expr.ToString());
    }

    [Fact]
    public void ConvertBinaryOperation_GreaterThanOrEqual_ReturnsZ3Ge()
    {
        var node = BinOp(TokenType.GreaterThanOrEqual, IntNode(3), IntNode(2));
        var expr = CreateMapper().ConvertExpr(node);
        
        Assert.IsType<Z3BoolExpr>(expr);
        Assert.Equal("(>= 3 2)", expr.ToString());
    }
}

