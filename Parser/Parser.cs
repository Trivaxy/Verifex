using Verifex.Parser.Nodes;

namespace Verifex.Parser;

public class Parser(TokenStream tokens, ReadOnlyMemory<char> source)
{
    private static readonly Dictionary<TokenType, Func<Parser, Token, AstNode>> PrefixParsers = new()
    {
        { TokenType.Minus, (parser, _) => new UnaryNegationNode(parser.Expression(0)) },
        { TokenType.Plus, (parser, _) => parser.Expression(0) },
        { TokenType.Number, (parser, token) => new NumberNode(int.Parse(parser.Fetch(token).Span)) },
        { TokenType.Identifier, (parser, token) => new IdentifierNode(parser.Fetch(token).ToString()) },
        { TokenType.LeftParenthesis, (parser, _) =>
        {
            AstNode expression = parser.Do(parser.Expression);
            parser.Expect(TokenType.RightParenthesis);

            return expression;
        }}
    };

    private static readonly Dictionary<TokenType, Func<Parser, AstNode, Token, AstNode>> InfixParsers = new()
    {
        { TokenType.Plus, InfixOp },
        { TokenType.Minus, InfixOp },
        { TokenType.Star, InfixOp },
        { TokenType.Slash, InfixOp },
        { TokenType.LeftParenthesis, (parser, left, _) =>
        {
            List<AstNode> parameters = [];
            while (parser.Peek().Type != TokenType.RightParenthesis)
            {
                parameters.Add(parser.Do(parser.Expression));
                if (parser.Peek().Type != TokenType.RightParenthesis)
                    parser.Expect(TokenType.Comma);
            }

            parser.Next(); // consume right parens

            return new FunctionCallNode(left, parameters.AsReadOnly());
        } }
    };

    private static readonly Dictionary<TokenType, int> TokenPrecedences = new()
    {
        { TokenType.Plus, 2 },
        { TokenType.Minus, 2 },
        { TokenType.Star, 3 },
        { TokenType.Slash, 3 },
        { TokenType.LeftParenthesis, 10 }
    };

    public ProgramNode Program()
    {
        List<AstNode> nodes = [];
        while (tokens.Peek().Type != TokenType.EOF)
        {
            nodes.Add(Do(FnDeclaration));
        }

        return new ProgramNode(nodes.AsReadOnly());
    }

    public AstNode Statement()
    {
        AstNode statement = Do<AstNode>(tokens.Peek().Type switch
        {
            TokenType.Let => LetDeclaration,
            _ => throw new Exception("Expected a statement")
        });

        Expect(TokenType.Semicolon);
        return statement;
    }

    public VarDeclNode LetDeclaration()
    {
        Expect(TokenType.Let);
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.Equals);
        AstNode value = Do(Expression);

        return new VarDeclNode(Fetch(name).ToString(), value);
    }

    public FunctionDeclNode FnDeclaration()
    {
        Expect(TokenType.Fn);
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.LeftParenthesis);

        List<TypedIdentifierNode> parameters = [];
        while (tokens.Peek().Type != TokenType.RightParenthesis)
        {
            parameters.Add(Do(TypedIdentifier));
            if (tokens.Peek().Type != TokenType.RightParenthesis)
                Expect(TokenType.Comma);
        }

        Expect(TokenType.RightParenthesis);
        
        Token? returnType = null;
        if (tokens.Peek().Type == TokenType.Arrow)
        {
            tokens.Next();
            returnType = Expect(TokenType.Identifier);
        }

        BlockNode body = Do(Block);
        string? returnTypeName = returnType.HasValue ? Fetch(returnType.Value).ToString() : null;
        return new FunctionDeclNode(Fetch(name).ToString(), parameters.AsReadOnly(), returnTypeName, body);
    }

    public TypedIdentifierNode TypedIdentifier()
    {
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.Colon);
        Token type = Expect(TokenType.Identifier);

        return new TypedIdentifierNode(Fetch(name).ToString(), Fetch(type).ToString());
    }

    public BlockNode Block()
    {
        Expect(TokenType.LeftCurlyBrace);

        List<AstNode> statements = [];
        while (tokens.Peek().Type != TokenType.RightCurlyBrace)
            statements.Add(Do(Statement));
        
        Expect(TokenType.RightCurlyBrace);

        return new BlockNode(statements.AsReadOnly());
    }

    public AstNode Expression() => Expression(0);

    public AstNode Expression(int precedence)
    {
        Token token = tokens.Next();
        
        if (!PrefixParsers.TryGetValue(token.Type, out var prefixParser))
            throw new Exception($"Parser `{token.Type}` is not supported");
        
        AstNode left = prefixParser(this, token);
        
        while (precedence < FetchTokenPrecedence())
        {
            token = tokens.Next();
            left = InfixParsers[token.Type](this, left, token);
        }

        return left;
    }

    private T Do<T>(Func<T> parser) where T : AstNode
    {
        var start = tokens.Peek().Range.Start;
        T node = parser();
        var end = tokens.Current.Range.End;

        node.Location = start..end;
        
        return node;
    }
    
    private Token Peek() => tokens.Peek();
    
    private Token Next() => tokens.Next();

    private ReadOnlyMemory<char> Fetch(Token token) => source[token.Range];

    private int FetchTokenPrecedence() => TokenPrecedences.GetValueOrDefault(Peek().Type, 0);

    private Token Expect(TokenType type)
    {
        Token next = Next();
        if (next.Type != type)
            throw new Exception("Expected " + type + " but got " + next.Type);
        
        return next;
    }

    private static AstNode InfixOp(Parser parser, AstNode left, Token token)
    {
        AstNode right = parser.Expression(TokenPrecedences[token.Type]);
        return new BinaryOperationNode(token, left, right);
    }
}