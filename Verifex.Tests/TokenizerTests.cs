using Verifex.Parsing;

namespace Verifex.Tests;

public class TokenizerTests
{
    [Fact]
    public void TokenStream_EmptyInput_ReturnsEOF()
    {
        // Arrange
        var tokenStream = new TokenStream("");
        
        // Act
        var token = tokenStream.Next();
        
        // Assert
        Assert.Equal(TokenType.EOF, token.Type);
    }
    
    [Fact]
    public void TokenStream_SimpleNumber_ReturnsNumberToken()
    {
        // Arrange
        var tokenStream = new TokenStream("123");
        
        // Act
        var token = tokenStream.Next();
        
        // Assert
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(0..3, token.Range);
    }
    
    [Fact]
    public void TokenStream_DecimalNumber_ReturnsNumberToken()
    {
        // Arrange
        var tokenStream = new TokenStream("123.456");
        
        // Act
        var token = tokenStream.Next();
        
        // Assert
        Assert.Equal(TokenType.Number, token.Type);
        Assert.Equal(0..7, token.Range);
    }
    
    [Fact]
    public void TokenStream_String_ReturnsStringToken()
    {
        // Arrange
        var tokenStream = new TokenStream("\"hello\"");
        
        // Act
        var token = tokenStream.Next();
        
        // Assert
        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal(0..7, token.Range);
    }
    
    [Fact]
    public void TokenStream_Identifier_ReturnsIdentifierToken()
    {
        // Arrange
        var tokenStream = new TokenStream("myVar");
        
        // Act
        var token = tokenStream.Next();
        
        // Assert
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal(0..5, token.Range);
    }
    
    [Fact]
    public void TokenStream_Keywords_ReturnsCorrectTokens()
    {
        // Arrange
        var tokenStream = new TokenStream("let mut fn -> return true false if else while type where");
        
        // Act & Assert
        Assert.Equal(TokenType.Let, tokenStream.Next().Type);
        Assert.Equal(TokenType.Mut, tokenStream.Next().Type);
        Assert.Equal(TokenType.Fn, tokenStream.Next().Type);
        Assert.Equal(TokenType.Arrow, tokenStream.Next().Type);
        Assert.Equal(TokenType.Return, tokenStream.Next().Type);
        Assert.Equal(TokenType.Bool, tokenStream.Next().Type);
        Assert.Equal(21..25, tokenStream.Current.Range); // "true"
        Assert.Equal(TokenType.Bool, tokenStream.Next().Type);
        Assert.Equal(26..31, tokenStream.Current.Range); // "false"
        Assert.Equal(TokenType.If, tokenStream.Next().Type);
        Assert.Equal(TokenType.Else, tokenStream.Next().Type);
        Assert.Equal(TokenType.While, tokenStream.Next().Type);
        Assert.Equal(TokenType.Type, tokenStream.Next().Type);
        Assert.Equal(TokenType.Where, tokenStream.Next().Type);
    }
    
    [Fact]
    public void TokenStream_Operators_ReturnsCorrectTokens()
    {
        // Arrange
        var tokenStream = new TokenStream("+ - * / = : ; , ( ) { } > < >= <= == != && || ! & |");
        
        // Act & Assert
        Assert.Equal(TokenType.Plus, tokenStream.Next().Type);
        Assert.Equal(TokenType.Minus, tokenStream.Next().Type);
        Assert.Equal(TokenType.Star, tokenStream.Next().Type);
        Assert.Equal(TokenType.Slash, tokenStream.Next().Type);
        Assert.Equal(TokenType.Equals, tokenStream.Next().Type);
        Assert.Equal(TokenType.Colon, tokenStream.Next().Type);
        Assert.Equal(TokenType.Semicolon, tokenStream.Next().Type);
        Assert.Equal(TokenType.Comma, tokenStream.Next().Type);
        Assert.Equal(TokenType.LeftParenthesis, tokenStream.Next().Type);
        Assert.Equal(TokenType.RightParenthesis, tokenStream.Next().Type);
        Assert.Equal(TokenType.LeftCurlyBrace, tokenStream.Next().Type);
        Assert.Equal(TokenType.RightCurlyBrace, tokenStream.Next().Type);
        Assert.Equal(TokenType.GreaterThan, tokenStream.Next().Type);
        Assert.Equal(TokenType.LessThan, tokenStream.Next().Type);
        Assert.Equal(TokenType.GreaterThanOrEqual, tokenStream.Next().Type);
        Assert.Equal(TokenType.LessThanOrEqual, tokenStream.Next().Type);
        Assert.Equal(TokenType.EqualEqual, tokenStream.Next().Type);
        Assert.Equal(TokenType.NotEqual, tokenStream.Next().Type);
        Assert.Equal(TokenType.And, tokenStream.Next().Type);
        Assert.Equal(TokenType.Or, tokenStream.Next().Type);
        Assert.Equal(TokenType.Not, tokenStream.Next().Type);
        Assert.Equal(TokenType.BitwiseAnd, tokenStream.Next().Type);
        Assert.Equal(TokenType.BitwiseOr, tokenStream.Next().Type);
    }
    
    [Fact]
    public void TokenStream_ComplexSource_TokenizesCorrectly()
    {
        // Arrange
        var source = "fn add(a: number, b: number) -> number {\n" +
                      "  let result: number = a + b;\n" +
                      "  result;\n" +
                      "}";
        var tokenStream = new TokenStream(source);
        
        // Act & Assert
        // fn
        Assert.Equal(TokenType.Fn, tokenStream.Next().Type);
        
        // add
        var token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // (
        Assert.Equal(TokenType.LeftParenthesis, tokenStream.Next().Type);
        
        // a
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // :
        Assert.Equal(TokenType.Colon, tokenStream.Next().Type);
        
        // number
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // ,
        Assert.Equal(TokenType.Comma, tokenStream.Next().Type);
        
        // b
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // :
        Assert.Equal(TokenType.Colon, tokenStream.Next().Type);
        
        // number
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // )
        Assert.Equal(TokenType.RightParenthesis, tokenStream.Next().Type);
        
        // ->
        Assert.Equal(TokenType.Arrow, tokenStream.Next().Type);
        
        // number
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // {
        Assert.Equal(TokenType.LeftCurlyBrace, tokenStream.Next().Type);
        
        // let
        Assert.Equal(TokenType.Let, tokenStream.Next().Type);
        
        // result
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // :
        Assert.Equal(TokenType.Colon, tokenStream.Next().Type);
        
        // number
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // =
        Assert.Equal(TokenType.Equals, tokenStream.Next().Type);
        
        // a
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // +
        Assert.Equal(TokenType.Plus, tokenStream.Next().Type);
        
        // b
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // ;
        Assert.Equal(TokenType.Semicolon, tokenStream.Next().Type);
        
        // result
        token = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, token.Type);
        
        // ;
        Assert.Equal(TokenType.Semicolon, tokenStream.Next().Type);
        
        // }
        Assert.Equal(TokenType.RightCurlyBrace, tokenStream.Next().Type);
        
        // EOF
        Assert.Equal(TokenType.EOF, tokenStream.Next().Type);
    }
    
    [Fact]
    public void TokenStream_PeekDoesNotAdvance()
    {
        // Arrange
        var tokenStream = new TokenStream("a b");
        
        // Act & Assert
        var peeked = tokenStream.Peek();
        Assert.Equal(TokenType.Identifier, peeked.Type);
        
        var next = tokenStream.Next();
        Assert.Equal(TokenType.Identifier, next.Type);
        Assert.Equal(peeked.Range, next.Range);
        
        peeked = tokenStream.Peek();
        Assert.Equal(TokenType.Identifier, peeked.Type);
        Assert.Equal(2..3, peeked.Range);
    }
    
    [Fact]
    public void TokenStream_EscapedCharactersInString()
    {
        // Arrange
        var tokenStream = new TokenStream("\"hello\\\"world\"");
        
        // Act
        var token = tokenStream.Next();
        
        // Assert
        Assert.Equal(TokenType.String, token.Type);
    }

    [Fact]
    public void TokenStream_UnknownTokens_ReturnsUnknownType()
    {
        // Arrange
        var tokenStream = new TokenStream("## @@#5let#");
        
        // Act & Assert
        Assert.Equal(TokenType.Unknown, tokenStream.Next().Type);
        Assert.Equal(TokenType.Unknown, tokenStream.Next().Type);
        Assert.Equal(TokenType.Number, tokenStream.Next().Type);
        Assert.Equal(TokenType.Let, tokenStream.Next().Type);
        Assert.Equal(TokenType.Unknown, tokenStream.Next().Type);
    }
    
    [Fact]
    public void TokenStream_Comment_Ignored()
    {
        // Arrange
        var tokenStream = new TokenStream("// This is a comment\n  //  \nlet // another\n/ / // another");
        
        // Act & Assert
        Assert.Equal(TokenType.Let, tokenStream.Next().Type);
        Assert.Equal(TokenType.Slash, tokenStream.Next().Type);
        Assert.Equal(TokenType.Slash, tokenStream.Next().Type);
        Assert.Equal(TokenType.EOF, tokenStream.Next().Type);
    }
}