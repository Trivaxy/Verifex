using Verifex.Analysis;
using Verifex.Parsing;

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
    public void Parse_BoolLiteral_ReturnsBoolNode()
    {
        var result = ParseExpression("true");
        
        Assert.IsType<BoolLiteralNode>(result);
        var boolNode = (BoolLiteralNode)result;
        Assert.True(boolNode.Value);
        
        result = ParseExpression("false");
        
        Assert.IsType<BoolLiteralNode>(result);
        boolNode = (BoolLiteralNode)result;
        Assert.False(boolNode.Value);
    }
    
    [Fact]
    public void Parse_Identifier_ReturnsIdentifierNode()
    {
        var result = ParseExpression("variable");
        
        Assert.IsType<IdentifierNode>(result);
        Assert.Equal("variable", ((IdentifierNode)result).Identifier);
    }
    
    [Fact]
    public void Parse_MinusNegation_ReturnsMinusNegationNode()
    {
        var result = ParseExpression("-42");
        
        Assert.IsType<MinusNegationNode>(result);
        var operand = ((MinusNegationNode)result).Operand;
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
    public void Parse_NotNegation_ReturnsNotNegationNode()
    {
        var result = ParseExpression("!true");
        
        Assert.IsType<NotNegationNode>(result);
        var operand = ((NotNegationNode)result).Operand;
        Assert.IsType<BoolLiteralNode>(operand);
        Assert.True(((BoolLiteralNode)operand).Value);
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
        var result = ParseExpression("a + b * c && d");
        
        // Should parse as: (a + (b * c)) && d
        Assert.IsType<BinaryOperationNode>(result);
        var andOp = (BinaryOperationNode)result;
        Assert.Equal(TokenType.And, andOp.Operator.Type);
        
        // Left side should be (a + (b * c))
        Assert.IsType<BinaryOperationNode>(andOp.Left);
        var plusOp = (BinaryOperationNode)andOp.Left;
        Assert.Equal(TokenType.Plus, plusOp.Operator.Type);
        Assert.IsType<IdentifierNode>(plusOp.Left);
        Assert.Equal("a", ((IdentifierNode)plusOp.Left).Identifier);
        
        // The multiplication should be the right operand of addition
        Assert.IsType<BinaryOperationNode>(plusOp.Right);
        var mulOp = (BinaryOperationNode)plusOp.Right;
        Assert.Equal(TokenType.Star, mulOp.Operator.Type);
        Assert.IsType<IdentifierNode>(mulOp.Left);
        Assert.Equal("b", ((IdentifierNode)mulOp.Left).Identifier);
        Assert.IsType<IdentifierNode>(mulOp.Right);
        Assert.Equal("c", ((IdentifierNode)mulOp.Right).Identifier);
        
        // Right side of && should be d
        Assert.IsType<IdentifierNode>(andOp.Right);
        Assert.Equal("d", ((IdentifierNode)andOp.Right).Identifier);
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
        Assert.Null(result.TypeHint);
        Assert.IsType<NumberNode>(result.Value);
    }
    
    [Fact]
    public void Parse_LetDeclarationWithType_ReturnsVarDeclNode()
    {
        var parser = CreateParser("let x: int = 42");
        var result = parser.LetDeclaration();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", result.Name);
        Assert.Equal("int", result.TypeHint);
        Assert.IsType<NumberNode>(result.Value);
    }
    
    [Fact]
    public void Parse_MutDeclaration_ReturnsVarDeclNodeWithMutableFlag()
    {
        var parser = CreateParser("mut x = 42");
        var result = parser.MutDeclaration();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", result.Name);
        Assert.Null(result.TypeHint);
        Assert.IsType<NumberNode>(result.Value);
        Assert.True(result.Mutable);
    }
    
    [Fact]
    public void Parse_MutDeclarationWithType_ReturnsVarDeclNodeWithMutableFlag()
    {
        var parser = CreateParser("mut x: int = 42");
        var result = parser.MutDeclaration();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", result.Name);
        Assert.Equal("int", result.TypeHint);
        Assert.IsType<NumberNode>(result.Value);
        Assert.True(result.Mutable);
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
    public void Parse_Statement_RecognizesMutDeclaration()
    {
        var parser = CreateParser("mut x = 42;");
        var result = parser.Statement();
        
        Assert.IsType<VarDeclNode>(result);
        Assert.Equal("x", ((VarDeclNode)result).Name);
        Assert.True(((VarDeclNode)result).Mutable);
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
    
    [Fact]
    public void Parse_Assignment_ReturnsAssignmentNode()
    {
        var parser = CreateParser("x = 42;");
        var result = parser.Statement();
        
        Assert.IsType<AssignmentNode>(result);
        Assert.IsType<IdentifierNode>(((AssignmentNode)result).Target);
        Assert.Equal("x", ((IdentifierNode)((AssignmentNode)result).Target).Identifier);
        Assert.IsType<NumberNode>(((AssignmentNode)result).Value);
    }

    [Fact]
    public void Parse_Assignment_WithMemberAccess()
    {
        var parser = CreateParser("x.foo().y = 42;");
        var result = parser.Statement();
        
        Assert.IsType<AssignmentNode>(result);
        var assignment = (AssignmentNode)result;
        
        // Check the target is a member access 
        Assert.IsType<MemberAccessNode>(assignment.Target);
        var memberAccess = (MemberAccessNode)assignment.Target;
        
        // Check the member access target is a function call
        Assert.IsType<FunctionCallNode>(memberAccess.Target);
        var functionCall = (FunctionCallNode)memberAccess.Target;
        
        // Check the function call is on a member access
        Assert.IsType<MemberAccessNode>(functionCall.Callee);
        var innerMemberAccess = (MemberAccessNode)functionCall.Callee;
        
        // Check the base object and function name
        Assert.IsType<IdentifierNode>(innerMemberAccess.Target);
        Assert.Equal("x", ((IdentifierNode)innerMemberAccess.Target).Identifier);
        Assert.Equal("foo", innerMemberAccess.Member.Identifier);
        
        // Check the final member being accessed
        Assert.Equal("y", memberAccess.Member.Identifier);
        
        // Check the assigned value
        Assert.IsType<NumberNode>(assignment.Value);
        Assert.Equal("42", ((NumberNode)assignment.Value).Value);
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

    [Fact]
    public void Parse_Program_RecognizesItems()
    {
        var source = "fn main() {} type Test = Int where value == 42;";
        var parser = CreateParser(source);
        var result = parser.Program();
        
        Assert.IsType<ProgramNode>(result);
        Assert.Equal(2, result.Nodes.Count);
        Assert.IsType<FunctionDeclNode>(result.Nodes[0]);
        Assert.IsType<RefinedTypeDeclNode>(result.Nodes[1]);
    }
    
    // Error handling tests
    [Fact]
    public void Parse_MissingClosingParenthesis_ThrowsException()
    {
        var parser = CreateParser("(1 + 2");
        parser.DoSafe(parser.Expression, []);
        Assert.Contains(parser.Diagnostics, d => d is ExpectedToken { What: ")" });
    }
    
    [Fact]
    public void Parse_UnsupportedPrefix_ThrowsException()
    {
        var parser = CreateParser("@");
        parser.DoSafe(parser.Expression, []);
        Assert.Contains(parser.Diagnostics, d => d is UnexpectedToken { What: "@" });
    }
    
    [Fact]
    public void Parse_InvalidStatement_ThrowsException()
    {
        var parser = CreateParser("42;");
        parser.DoSafe(parser.Statement, []);
        Assert.Contains(parser.Diagnostics, d => d is UnexpectedToken { What: "42" });
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
        Assert.Equal(@"unknown \escape \x sequence", stringNode.Value);
    }
    
    // Error reporting and recovery tests
    [Fact]
    public void Parse_ErrorAddsDiagnostic_RecordsDiagnostic()
    {
        var parser = CreateParser("let x = @;");
        
        try 
        {
            parser.Statement();
        }
        catch { /* Ignore exception */ }
        
        Assert.NotEmpty(parser.Diagnostics);
        Assert.Equal(DiagnosticLevel.Error, parser.Diagnostics[0].Level);
    }
    
    [Fact]
    public void Parse_MismatchedParenthesis_RecordsDiagnostic()
    {
        // Missing closing parenthesis after parameter list
        var parser = CreateParser("fn test(a: int, b: int { let sum = a + b; return sum; }");
        
        var result = parser.FnDeclaration();
        
        // Verify diagnostics
        Assert.NotEmpty(parser.Diagnostics);
        var diagnostic = parser.Diagnostics.FirstOrDefault(d => d.Message.Contains("expected )"));
        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticLevel.Error, diagnostic.Level);
        
        // Verify recovery - function should still parse correctly
        Assert.Equal("test", result.Name);
        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("a", result.Parameters[0].Identifier);
        Assert.Equal("int", result.Parameters[0].TypeName);
        Assert.Equal("b", result.Parameters[1].Identifier);
        Assert.Equal("int", result.Parameters[1].TypeName);
        
        // Verify the body was still parsed correctly despite the error
        Assert.NotNull(result.Body);
        Assert.Equal(2, result.Body.Nodes.Count);
        Assert.IsType<VarDeclNode>(result.Body.Nodes[0]);
        Assert.IsType<ReturnNode>(result.Body.Nodes[1]);
    }
    
    [Fact]
    public void Parse_RecoveryInBlock_ContinuesParsing()
    {
        var parser = CreateParser("{ let x = 1; let y = @%$; let z = 3; }");
        
        var block = parser.Block();
        
        Assert.NotEmpty(parser.Diagnostics);
        // Should recover and continue parsing
        Assert.Equal(2, block.Nodes.Count);
        Assert.Equal("x", ((VarDeclNode)block.Nodes[0]).Name);
        Assert.Equal("z", ((VarDeclNode)block.Nodes[1]).Name);
    }
    
    [Fact]
    public void Parse_RecoveryInParameter_ContinuesParsing()
    {
        var parser = CreateParser("fn test(a: int, b: , c: int) { }");
        
        var result = parser.FnDeclaration();
        
        Assert.NotEmpty(parser.Diagnostics);
        // Should recover and continue parsing
        Assert.Equal(2, result.Parameters.Count);
        Assert.Equal("a", result.Parameters[0].Identifier);
        Assert.Equal("c", result.Parameters[1].Identifier);
    }
    
    [Fact]
    public void Parse_MultipleErrors_RecordsAllDiagnostics()
    {
        var source = "fn test(a: ) { let x = @; return }";
        var parser = CreateParser(source);
        
        parser.FnDeclaration();
        
        Assert.True(parser.Diagnostics.Count >= 2);
    }
    
    [Fact]
    public void Parse_ErrorInProgram_RecoversToParseFunctions()
    {
        var source = "fn valid() { return 1; } invalid token fn another() { return 2; }";
        var parser = CreateParser(source);
        
        var program = parser.Program();
        
        Assert.NotEmpty(parser.Diagnostics);
        Assert.Equal(2, program.Nodes.Count);
        Assert.Equal("valid", ((FunctionDeclNode)program.Nodes[0]).Name);
        Assert.Equal("another", ((FunctionDeclNode)program.Nodes[1]).Name);
    }
    
    [Fact]
    public void Parse_DiagnosticMessage_ContainsLocationInfo()
    {
        var source = "fn test() { let x = @; }";
        var parser = CreateParser(source);
        
        try 
        {
            parser.FnDeclaration();
        }
        catch { /* Ignore exception */ }
        
        Assert.NotEmpty(parser.Diagnostics);
        var diagnostic = parser.Diagnostics[0];
        Assert.True(diagnostic.Location.Start.Value > 0);
        Assert.True(diagnostic.Location.End.Value > diagnostic.Location.Start.Value);
        
        var message = diagnostic.GetMessage(source.AsSpan());
        Assert.Contains("error:", message);
        Assert.Contains("@", message);
    }
    
    // If-Else statement tests
    [Fact]
    public void Parse_IfStatement_ReturnsIfElseNode()
    {
        var parser = CreateParser("if (true) { let x = 42; }");
        var result = parser.IfStatement();
        
        Assert.IsType<IfElseNode>(result);
        Assert.IsType<BoolLiteralNode>(result.Condition);
        Assert.True(((BoolLiteralNode)result.Condition).Value);
        Assert.IsType<BlockNode>(result.IfBody);
        Assert.Single(result.IfBody.Nodes);
        Assert.IsType<VarDeclNode>(result.IfBody.Nodes[0]);
        Assert.Null(result.ElseBody);
    }
    
    [Fact]
    public void Parse_IfElseStatement_ReturnsIfElseNodeWithElseBody()
    {
        var parser = CreateParser("if (true) { let x = 42; } else { let y = 24; }");
        var result = parser.IfStatement();
        
        Assert.IsType<IfElseNode>(result);
        Assert.IsType<BoolLiteralNode>(result.Condition);
        Assert.True(((BoolLiteralNode)result.Condition).Value);
        Assert.IsType<BlockNode>(result.IfBody);
        Assert.Single(result.IfBody.Nodes);
        Assert.IsType<VarDeclNode>(result.IfBody.Nodes[0]);
        
        Assert.NotNull(result.ElseBody);
        Assert.IsType<BlockNode>(result.ElseBody);
        Assert.Single(result.ElseBody.Nodes);
        Assert.IsType<VarDeclNode>(result.ElseBody.Nodes[0]);
    }
    
    [Fact]
    public void Parse_IfElseIfStatement_ReturnsNestedStructure()
    {
        var parser = CreateParser("if (true) { let x = 1; } else if (false) { let y = 2; }");
        var result = parser.IfStatement();
        
        Assert.IsType<IfElseNode>(result);
        Assert.NotNull(result.ElseBody);
        Assert.Single(result.ElseBody.Nodes);
        
        // The else body should contain a nested if statement
        Assert.IsType<IfElseNode>(result.ElseBody.Nodes[0]);
        var nestedIf = (IfElseNode)result.ElseBody.Nodes[0];
        Assert.IsType<BoolLiteralNode>(nestedIf.Condition);
        Assert.False(((BoolLiteralNode)nestedIf.Condition).Value);
    }
    
    [Fact]
    public void Parse_ComplexIfElseChain_ParsesCorrectly()
    {
        var source = @"
        if (a > b) {
            let max = a;
        } else if (b > a) {
            let max = b;
        } else {
            let max = a; // they're equal
        }";
        
        var parser = CreateParser(source);
        var result = parser.IfStatement();
        
        Assert.IsType<IfElseNode>(result);
        Assert.NotNull(result.ElseBody);
        Assert.Single(result.ElseBody.Nodes);
        
        // First else contains an if
        var elseIf = (IfElseNode)result.ElseBody.Nodes[0];
        Assert.NotNull(elseIf.ElseBody);
    }
    
    [Fact]
    public void Parse_Statement_RecognizesIfStatement()
    {
        var parser = CreateParser("if (true) { return 42; }");
        var result = parser.Statement();
        
        Assert.IsType<IfElseNode>(result);
    }
    
    [Fact]
    public void Parse_WhileStatement_ReturnsWhileNode()
    {
        var parser = CreateParser("while (true) { let x = 1; }");
        var result = parser.Statement();

        Assert.IsType<WhileNode>(result);
        
        var whileNode = (WhileNode)result;
        Assert.IsType<BoolLiteralNode>(whileNode.Condition);
        Assert.True(((BoolLiteralNode)whileNode.Condition).Value);
        Assert.IsType<BlockNode>(whileNode.Body);
        Assert.Single(whileNode.Body.Nodes);
        Assert.IsType<VarDeclNode>(whileNode.Body.Nodes[0]);
    }

    [Fact]
    public void Parse_RefinedType_ReturnsRefinedTypeDeclNode()
    {
        var parser = CreateParser("type NonZeroInt = Int where value > 0");
        var result = parser.RefinedTypeDeclaration();

        Assert.IsType<RefinedTypeDeclNode>(result);
        Assert.Equal("NonZeroInt", result.Name);
        Assert.Equal("Int", result.BaseType);
        Assert.IsType<BinaryOperationNode>(result.Expression);
    }

    // Struct parsing tests
    [Fact]
    public void Parse_StructDeclaration_ReturnsStructDeclNode()
    {
        var parser = CreateParser("struct Person { name: String, age: Int }");
        var result = parser.StructDeclaration();
        
        Assert.IsType<StructDeclNode>(result);
        Assert.Equal("Person", result.Name);
        Assert.Equal(2, result.Fields.Count);
        Assert.Equal("name", result.Fields[0].Name);
        Assert.Equal("String", result.Fields[0].Type);
        Assert.Equal("age", result.Fields[1].Name);
        Assert.Equal("Int", result.Fields[1].Type);
    }
    
    [Fact]
    public void Parse_StructDeclarationWithTrailingComma_ReturnsStructDeclNode()
    {
        var parser = CreateParser("struct Point { x: Int, y: Int, }");
        var result = parser.StructDeclaration();
        
        Assert.IsType<StructDeclNode>(result);
        Assert.Equal("Point", result.Name);
        Assert.Equal(2, result.Fields.Count);
        Assert.Equal("x", result.Fields[0].Name);
        Assert.Equal("Int", result.Fields[0].Type);
        Assert.Equal("y", result.Fields[1].Name);
        Assert.Equal("Int", result.Fields[1].Type);
    }
    
    [Fact]
    public void Parse_EmptyStructDeclaration_ReturnsEmptyStructDeclNode()
    {
        var parser = CreateParser("struct Empty { }");
        var result = parser.StructDeclaration();
        
        Assert.IsType<StructDeclNode>(result);
        Assert.Equal("Empty", result.Name);
        Assert.Empty(result.Fields);
    }
    
    [Fact]
    public void Parse_MalformedStructDeclaration_RecoversProperly()
    {
        var parser = CreateParser("struct Malformed { name: String age: Int }");
        var result = parser.StructDeclaration();
        
        Assert.IsType<StructDeclNode>(result);
        Assert.Equal("Malformed", result.Name);
        Assert.Equal(2, result.Fields.Count);
        Assert.Equal("name", result.Fields[0].Name);
        Assert.Equal("String", result.Fields[0].Type);
        Assert.Equal("age", result.Fields[1].Name);
        Assert.Equal("Int", result.Fields[1].Type);
        Assert.NotEmpty(parser.Diagnostics); // Should report an error
    }

    [Fact]
    public void Parse_StructAsItem_RecognizedInProgram()
    {
        var source = "struct User { id: Int, name: String }\nfn main() { }";
        var parser = CreateParser(source);
        var result = parser.Program();
        
        Assert.Equal(2, result.Nodes.Count);
        Assert.IsType<StructDeclNode>(result.Nodes[0]);
        Assert.IsType<FunctionDeclNode>(result.Nodes[1]);
        
        var structNode = (StructDeclNode)result.Nodes[0];
        Assert.Equal("User", structNode.Name);
        Assert.Equal(2, structNode.Fields.Count);
    }

    [Fact]
    public void Parse_StructWithMethods_ReturnsStructDeclNode()
    {
        var parser = CreateParser("struct Point { x: Int, y: Int, fn distance() { return sqrt(x * x + y * y); } }");
        var result = parser.StructDeclaration();
        
        Assert.Equal("Point", result.Name);
        Assert.Equal(2, result.Fields.Count);
        Assert.Equal("x", result.Fields[0].Name);
        Assert.Equal("Int", result.Fields[0].Type);
        Assert.Equal("y", result.Fields[1].Name);
        Assert.Equal("Int", result.Fields[1].Type);
        
        Assert.Single(result.Methods);
        Assert.Equal("distance", result.Methods[0].Function.Name);
    }

    [Fact]
    public void Parse_MemberAccess_ReturnsMemberAccessNode()
    {
        var result = ParseExpression("obj.field");
        
        Assert.IsType<MemberAccessNode>(result);
        var memberAccess = (MemberAccessNode)result;
        
        Assert.IsType<IdentifierNode>(memberAccess.Target);
        Assert.Equal("obj", ((IdentifierNode)memberAccess.Target).Identifier);
        
        Assert.IsType<IdentifierNode>(memberAccess.Member);
        Assert.Equal("field", memberAccess.Member.Identifier);
    }
    
    [Fact]
    public void Parse_ChainedMemberAccess_ReturnsMemberAccessNode()
    {
        var result = ParseExpression("obj.field1.field2");
        
        Assert.IsType<MemberAccessNode>(result);
        var memberAccess = (MemberAccessNode)result;
        
        Assert.IsType<MemberAccessNode>(memberAccess.Target);
        var innerMemberAccess = (MemberAccessNode)memberAccess.Target;
        
        Assert.IsType<IdentifierNode>(innerMemberAccess.Target);
        Assert.Equal("obj", ((IdentifierNode)innerMemberAccess.Target).Identifier);
        Assert.Equal("field1", innerMemberAccess.Member.Identifier);
        
        Assert.Equal("field2", memberAccess.Member.Identifier);
    }

    [Fact]
    public void Parse_MemberAccessWithFunctionCall_ReturnsMemberAccessNode()
    {
        var result = ParseExpression("obj.getField().value");
        
        Assert.IsType<MemberAccessNode>(result);
        var memberAccess = (MemberAccessNode)result;
        
        Assert.IsType<FunctionCallNode>(memberAccess.Target);
        var functionCall = (FunctionCallNode)memberAccess.Target;
        
        Assert.IsType<MemberAccessNode>(functionCall.Callee);
        var innerMemberAccess = (MemberAccessNode)functionCall.Callee;
        
        Assert.IsType<IdentifierNode>(innerMemberAccess.Target);
        Assert.Equal("obj", ((IdentifierNode)innerMemberAccess.Target).Identifier);
        Assert.Equal("getField", innerMemberAccess.Member.Identifier);
        
        Assert.IsType<IdentifierNode>(memberAccess.Member);
        Assert.Equal("value", memberAccess.Member.Identifier);
    }

    // Struct initializer tests
    [Fact]
    public void Parse_StructInitializer_ReturnsInitializerNode()
    {
        var result = ParseExpression("Point { x: 10, y: 20 }");
        
        Assert.IsType<InitializerNode>(result);
        var initializer = (InitializerNode)result;
        
        Assert.IsType<IdentifierNode>(initializer.Type);
        Assert.Equal("Point", ((IdentifierNode)initializer.Type).Identifier);
        
        Assert.IsType<InitializerListNode>(initializer.InitializerList);
        var initializerList = initializer.InitializerList;
        
        Assert.Equal(2, initializerList.Values.Count);
        Assert.Equal("x", initializerList.Values[0].Name.Identifier);
        Assert.IsType<NumberNode>(initializerList.Values[0].Value);
        Assert.Equal("10", ((NumberNode)initializerList.Values[0].Value).Value);
        
        Assert.Equal("y", initializerList.Values[1].Name.Identifier);
        Assert.IsType<NumberNode>(initializerList.Values[1].Value);
        Assert.Equal("20", ((NumberNode)initializerList.Values[1].Value).Value);
    }
    
    [Fact]
    public void Parse_StructInitializerWithTrailingComma_ReturnsInitializerNode()
    {
        var result = ParseExpression("Point { x: 10, y: 20, }");
        
        Assert.IsType<InitializerNode>(result);
        var initializer = (InitializerNode)result;
        
        Assert.Equal("Point", ((IdentifierNode)initializer.Type).Identifier);
        Assert.Equal(2, initializer.InitializerList.Values.Count);
        Assert.Equal("x", initializer.InitializerList.Values[0].Name.Identifier);
        Assert.Equal("y", initializer.InitializerList.Values[1].Name.Identifier);
    }
    
    [Fact]
    public void Parse_EmptyStructInitializer_ReturnsInitializerNode()
    {
        var result = ParseExpression("EmptyPoint { }");
        
        Assert.IsType<InitializerNode>(result);
        var initializer = (InitializerNode)result;
        
        Assert.Equal("EmptyPoint", ((IdentifierNode)initializer.Type).Identifier);
        Assert.Empty(initializer.InitializerList.Values);
    }
    
    [Fact]
    public void Parse_NestedStructInitializer_ReturnsNestedInitializerNode()
    {
        var result = ParseExpression("Rectangle { topLeft: Point { x: 0, y: 0 }, bottomRight: Point { x: 10, y: 20 } }");
        
        Assert.IsType<InitializerNode>(result);
        var initializer = (InitializerNode)result;
        
        Assert.Equal("Rectangle", ((IdentifierNode)initializer.Type).Identifier);
        Assert.Equal(2, initializer.InitializerList.Values.Count);
        
        Assert.Equal("topLeft", initializer.InitializerList.Values[0].Name.Identifier);
        Assert.IsType<InitializerNode>(initializer.InitializerList.Values[0].Value);
        
        var nestedPoint1 = (InitializerNode)initializer.InitializerList.Values[0].Value;
        Assert.Equal("Point", ((IdentifierNode)nestedPoint1.Type).Identifier);
        Assert.Equal(2, nestedPoint1.InitializerList.Values.Count);
        Assert.Equal("x", nestedPoint1.InitializerList.Values[0].Name.Identifier);
        Assert.Equal("0", ((NumberNode)nestedPoint1.InitializerList.Values[0].Value).Value);
        
        Assert.Equal("bottomRight", initializer.InitializerList.Values[1].Name.Identifier);
        Assert.IsType<InitializerNode>(initializer.InitializerList.Values[1].Value);
        
        var nestedPoint2 = (InitializerNode)initializer.InitializerList.Values[1].Value;
        Assert.Equal("Point", ((IdentifierNode)nestedPoint2.Type).Identifier);
        Assert.Equal(2, nestedPoint2.InitializerList.Values.Count);
        Assert.Equal("x", nestedPoint2.InitializerList.Values[0].Name.Identifier);
        Assert.Equal("10", ((NumberNode)nestedPoint2.InitializerList.Values[0].Value).Value);
    }
}

