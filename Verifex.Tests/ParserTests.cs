using Verifex.Parsing;
using Verifex.Parsing.Nodes;

namespace Verifex.Tests;

public class ParserTests
{
    private Parser CreateParser(string source)
    {
        var tokenStream = new TokenStream(source);
        return new Parser(tokenStream, source.AsMemory());
    }
    
    private AstNode ParseExpression(string source)
    {
        var parser = CreateParser(source);
        return parser.Expression();
    }
    
    // Expression parsing tests
    [Fact]
    public void Parse_NumberLiteral_ReturnsNumberNode()
    {
        var result = ParseExpression("42");
        
        Assert.IsType<NumberNode>(result);
        var numberNode = (NumberNode)result;
        Assert.Equal("42", numberNode.Value);
        Assert.Equal(NumberType.Integer, numberNode.NumberType);
    }
    
    [Fact]
    public void Parse_DecimalNumber_ReturnsNumberNode()
    {
        var result = ParseExpression("42.5");
        
        Assert.IsType<NumberNode>(result);
        var numberNode = (NumberNode)result;
        Assert.Equal("42.5", numberNode.Value);
        Assert.Equal(NumberType.Real, numberNode.NumberType);
    }
    
    [Fact]
    public void Parse_Identifier_ReturnsIdentifierNode()
    {
        var result = ParseExpression("variable");
        
        Assert.IsType<IdentifierNode>(result);
        Assert.Equal("variable", ((IdentifierNode)result).Identifier);
    }
    
    [Fact]
    public void Parse_UnaryNegation_ReturnsUnaryNegationNode()
    {
        var result = ParseExpression("-42");
        
        Assert.IsType<UnaryNegationNode>(result);
        var operand = ((UnaryNegationNode)result).Operand;
        Assert.IsType<NumberNode>(operand);
        Assert.Equal("42", ((NumberNode)operand).Value);
    }
    
    [Fact]
    public void Parse_UnaryPlus_IgnoresPlus()
    {
        var result = ParseExpression("+42");
        
        Assert.IsType<NumberNode>(result);
        Assert.Equal("42", ((NumberNode)result).Value);
    }
    
    [Fact]
    public void Parse_ParenthesizedExpression_ReturnsCorrectNode()
    {
        var result = ParseExpression("(42)");
        
        Assert.IsType<NumberNode>(result);
        Assert.Equal("42", ((NumberNode)result).Value);
    }
    
    // Binary operation tests
    [Fact]
    public void Parse_Addition_ReturnsBinaryOperationNode()
    {
        var result = ParseExpression("a + b");
        
        Assert.IsType<BinaryOperationNode>(result);
        var binOp = (BinaryOperationNode)result;
        Assert.Equal(TokenType.Plus, binOp.Operator.Type);
        Assert.Equal("a", ((IdentifierNode)binOp.Left).Identifier);
        Assert.Equal("b", ((IdentifierNode)binOp.Right).Identifier);
    }
    
    [Fact]
    public void Parse_OperatorPrecedence_RespectsCorrectOrder()
    {
        var result = ParseExpression("a + b * c");
        
        Assert.IsType<BinaryOperationNode>(result);
        var binOp = (BinaryOperationNode)result;
        Assert.Equal(TokenType.Plus, binOp.Operator.Type);
        Assert.IsType<IdentifierNode>(binOp.Left);
        Assert.IsType<BinaryOperationNode>(binOp.Right);
        
        var rightBinOp = (BinaryOperationNode)binOp.Right;
        Assert.Equal(TokenType.Star, rightBinOp.Operator.Type);
        Assert.Equal("b", ((IdentifierNode)rightBinOp.Left).Identifier);
        Assert.Equal("c", ((IdentifierNode)rightBinOp.Right).Identifier);
    }
    
    [Fact]
    public void Parse_ParenthesizedExpression_OverridesPrecedence()
    {
        var result = ParseExpression("(a + b) * c");
        
        Assert.IsType<BinaryOperationNode>(result);
        var binOp = (BinaryOperationNode)result;
        Assert.Equal(TokenType.Star, binOp.Operator.Type);
        Assert.IsType<BinaryOperationNode>(binOp.Left);
        Assert.IsType<IdentifierNode>(binOp.Right);
    }
    
    // Function call tests
    [Fact]
    public void Parse_FunctionCall_ReturnsFunctionCallNode()
    {
        var result = ParseExpression("add(x, y)");
        
        Assert.IsType<FunctionCallNode>(result);
        var call = (FunctionCallNode)result;
        Assert.IsType<IdentifierNode>(call.Callee);
        Assert.Equal("add", ((IdentifierNode)call.Callee).Identifier);
        Assert.Equal(2, call.Arguments.Count);
        Assert.Equal("x", ((IdentifierNode)call.Arguments[0]).Identifier);
        Assert.Equal("y", ((IdentifierNode)call.Arguments[1]).Identifier);
    }
    
    [Fact]
    public void Parse_EmptyFunctionCall_ReturnsCorrectNode()
    {
        var result = ParseExpression("print()");
        
        Assert.IsType<FunctionCallNode>(result);
        var call = (FunctionCallNode)result;
        Assert.Empty(call.Arguments);
    }
    
    // Variable declaration tests
    [Fact]
    public void Parse_LetDeclaration_ReturnsVarDeclNode()
    {
        var parser = CreateParser("let x = 42");
        var result = parser.LetDeclaration();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", result.Name);
        Assert.Null(result.Type);
        Assert.IsType<NumberNode>(result.Value);
    }
    
    [Fact]
    public void Parse_LetDeclarationWithType_ReturnsVarDeclNode()
    {
        var parser = CreateParser("let x: int = 42");
        var result = parser.LetDeclaration();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", result.Name);
        Assert.Equal("int", result.Type);
        Assert.IsType<NumberNode>(result.Value);
    }
    
    // Function declaration tests
    [Fact]
    public void Parse_FnDeclaration_ReturnsFunctionDeclNode()
    {
        var parser = CreateParser("fn add(a: int, b: int) -> int { let result = a + b; }");
        var result = parser.FnDeclaration();
        
        Assert.IsType<FunctionDeclNode>(result);
        Assert.Equal("add", result.Name);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("a", result.Parameters[0].Identifier);
        Assert.Equal("int", result.Parameters[0].TypeName);
        Assert.Equal("int", result.ReturnType);
        Assert.IsType<BlockNode>(result.Body);
        Assert.Single(result.Body.Nodes);
    }
    
    [Fact]
    public void Parse_FnDeclarationNoParams_ReturnsFunctionDeclNode()
    {
        var parser = CreateParser("fn hello() { print(\"hello\"); }");
        var result = parser.FnDeclaration();
        
        Assert.IsType<FunctionDeclNode>(result);
        Assert.Empty(result.Parameters);
        Assert.Empty(result.Parameters);
        Assert.Empty(result.Parameters);
        Assert.Null(result.ReturnType);
    }
    
    // Block tests
    [Fact]
    public void Parse_Block_ReturnsBlockNode()
    {
        var parser = CreateParser("{ let x = 1; let y = 2; }");
        var result = parser.Block();
        
        Assert.IsType<BlockNode>(result);
        Assert.Equal(2, result.Nodes.Count);
        Assert.IsType<VarDeclNode>(result.Nodes[0]);
        Assert.IsType<VarDeclNode>(result.Nodes[1]);
    }
    
    // Statement tests
    [Fact]
    public void Parse_Statement_RecognizesLetStatement()
    {
        var parser = CreateParser("let x = 42;");
        var result = parser.Statement();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", ((VarDeclNode)result).Name);
    }
    
    [Fact]
    public void Parse_Statement_RecognizesFunctionCallStatement()
    {
        var parser = CreateParser("print(42);");
        var result = parser.Statement();
        
        Assert.IsType<FunctionCallNode>(result);
        Assert.Equal("print", ((IdentifierNode)((FunctionCallNode)result).Callee).Identifier);
    }
    
    [Fact]
    public void Parse_Statement_RecognizesReturnStatement()
    {
        var parser = CreateParser("return 42;");
        var result = parser.Statement();
        
        Assert.IsType<ReturnNode>(result);
        Assert.IsType<NumberNode>(((ReturnNode)result).Value);
    }
    
    // Program tests
    [Fact]
    public void Parse_Program_ReturnsProgramNode()
    {
        var source = "fn main() { let x = 42; print(x); }";
        var parser = CreateParser(source);
        var result = parser.Program();
        
        Assert.IsType<ProgramNode>(result);
        Assert.Single(result.Nodes);
        Assert.IsType<FunctionDeclNode>(result.Nodes[0]);
    }
    
    // Error handling tests
    [Fact]
    public void Parse_MissingClosingParenthesis_ThrowsException()
    {
        var parser = CreateParser("(1 + 2");
        var ex = Assert.Throws<Exception>(() => parser.Expression());
        Assert.Contains("Expected RightParenthesis", ex.Message);
    }
    
    [Fact]
    public void Parse_UnsupportedPrefix_ThrowsException()
    {
        var parser = CreateParser("@");
        var ex = Assert.Throws<Exception>(() => parser.Expression());
        Assert.Contains("is not supported", ex.Message);
    }
    
    [Fact]
    public void Parse_InvalidStatement_ThrowsException()
    {
        var parser = CreateParser("42;");
        var ex = Assert.Throws<Exception>(() => parser.Statement());
        Assert.Contains("Expected a statement", ex.Message);
    }
    
    
    // String parsing tests
    [Fact]
    public void Parse_StringLiteral_ReturnsStringLiteralNode()
    {
        var result = ParseExpression("""
                                     "hello world"
                                     """);

        Assert.IsType<StringLiteralNode>(result);
        var stringNode = (StringLiteralNode)result;
        Assert.Equal("hello world", stringNode.Value);
    }

    [Fact]
    public void Parse_StringLiteralWithEscapes_HandlesEscapeSequencesCorrectly()
    {
        var result = ParseExpression("""
                                     "hello\nworld\t\"escaped\"\\path"
                                     """);
    
        Assert.IsType<StringLiteralNode>(result);
        var stringNode = (StringLiteralNode)result;
        Assert.Equal("hello\nworld\t\"escaped\"\\path", stringNode.Value);
    }
    
    [Fact]
    public void Parse_StringLiteralWithAllEscapes_HandlesAllEscapeSequences()
    {
        var result = ParseExpression("""
                                     "\n\t\r\"\\"
                                     """);
    
        Assert.IsType<StringLiteralNode>(result);
        var stringNode = (StringLiteralNode)result;
        Assert.Equal("\n\t\r\"\\", stringNode.Value);
    }
    
    [Fact]
    public void Parse_StringLiteralWithUnknownEscape_PreservesBackslash()
    {
        var result = ParseExpression("""
                                     "unknown \escape \x sequence"
                                     """);
    
        Assert.IsType<StringLiteralNode>(result);
        var stringNode = (StringLiteralNode)result;
        Assert.Equal("unknown \\escape \\x sequence", stringNode.Value);
    }
}