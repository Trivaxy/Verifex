using System.Collections.ObjectModel;
using System.Text;
using Verifex.Analysis;

namespace Verifex.Parsing;

public class Parser(TokenStream tokens, ReadOnlyMemory<char> source)
{
    private readonly List<CompileDiagnostic> _diagnostics = [];
    
    public ReadOnlyCollection<CompileDiagnostic> Diagnostics => _diagnostics.AsReadOnly();
    
    // Synchronization token sets for error recovery
    private static readonly HashSet<TokenType> StatementSyncTokens =
    [
        TokenType.Let, TokenType.Return, TokenType.Identifier, TokenType.RightCurlyBrace
    ];
    
    private static readonly HashSet<TokenType> DeclarationSyncTokens = [TokenType.Fn, TokenType.EOF];
    
    private static readonly HashSet<TokenType> ParameterSyncTokens = [
        TokenType.RightParenthesis, TokenType.Identifier, TokenType.Arrow, TokenType.LeftCurlyBrace
    ];
    
    private static readonly Dictionary<TokenType, Func<Parser, Token, AstNode>> PrefixParsers = new()
    {
        { TokenType.Minus, (parser, _) => new MinusNegationNode(parser.Expression(0)) },
        { TokenType.Not, (parser, _) => new NotNegationNode(parser.Expression(0)) },
        { TokenType.Plus, (parser, _) => parser.Expression(0) },
        { TokenType.Number, (parser, token) => new NumberNode(parser.Fetch(token).ToString()) { Location = token.Range } },
        { TokenType.Identifier, (parser, token) => new IdentifierNode(parser.Fetch(token).ToString()) { Location = token.Range } },
        { TokenType.Bool, (parser, token) => new BoolLiteralNode(bool.Parse(parser.Fetch(token).ToString())) { Location = token.Range } },
        { TokenType.String, (parser, token) => new StringLiteralNode(ProcessEscapes(parser.Fetch(token).ToString()[1..^1])) { Location = token.Range } },
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
        { TokenType.GreaterThan, InfixOp },
        { TokenType.LessThan, InfixOp },
        { TokenType.GreaterThanOrEqual, InfixOp },
        { TokenType.LessThanOrEqual, InfixOp },
        { TokenType.EqualEqual, InfixOp },
        { TokenType.NotEqual, InfixOp },
        { TokenType.Or, InfixOp },
        { TokenType.And, InfixOp },
        { TokenType.LeftParenthesis, (parser, left, _) =>
        {
            List<AstNode> parameters = [];
            if (parser.Peek().Type != TokenType.RightParenthesis)
            {
                parameters.Add(parser.Do(parser.Expression));
                
                while (parser.Peek().Type == TokenType.Comma)
                {
                    parser.Next();
                    parameters.Add(parser.Do(parser.Expression));
                }
            }
            
            parser.Expect(TokenType.RightParenthesis);

            return new FunctionCallNode(left, parameters.AsReadOnly());
        } }
    };

    private static readonly Dictionary<TokenType, int> TokenPrecedences = new()
    {
        { TokenType.Or, 1 },
        { TokenType.And, 2 },
        { TokenType.EqualEqual, 3 },
        { TokenType.NotEqual, 3 },
        { TokenType.GreaterThan, 4 },
        { TokenType.LessThan, 4 },
        { TokenType.GreaterThanOrEqual, 4 },
        { TokenType.LessThanOrEqual, 4 },
        { TokenType.Plus, 5 },
        { TokenType.Minus, 5 },
        { TokenType.Star, 6 },
        { TokenType.Slash, 6 },
        { TokenType.LeftParenthesis, 10 }
    };

    public ProgramNode Program()
    {
        List<AstNode> nodes = [];
        while (tokens.Peek().Type != TokenType.EOF)
        {
            AstNode? function = DoSafe(FnDeclaration, DeclarationSyncTokens);
            if (function is not null)
                nodes.Add(function);
        }

        return new ProgramNode(nodes.AsReadOnly());
    }

    public AstNode Statement()
    {
        AstNode statement = Do<AstNode>(tokens.Peek().Type switch
        {
            TokenType.Let => LetDeclaration,
            TokenType.Identifier => () =>
            {
                AstNode expr = Do(Expression);

                if (expr is not FunctionCallNode)
                    ThrowError(new ExpectedToken("statement") { Location = expr.Location });

                return expr;
            },
            TokenType.Return => Return,
            _ => () => ThrowError(new UnexpectedToken(Fetch(tokens.Peek()).ToString()) { Location = tokens.Peek().Range })
        });

        Expect(TokenType.Semicolon);
        return statement;
    }

    public VarDeclNode LetDeclaration()
    {
        Expect(TokenType.Let);
        Token name = Expect(TokenType.Identifier);
        string? type = null;
        
        if (tokens.Peek().Type == TokenType.Colon)
        {
            tokens.Next(); // consume colon
            type = Fetch(Expect(TokenType.Identifier)).ToString();
        }
        
        Expect(TokenType.Equals);
        AstNode value = Do(Expression);

        return new VarDeclNode(Fetch(name).ToString(), type, value);
    }

    public FunctionDeclNode FnDeclaration()
    {
        Expect(TokenType.Fn);
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.LeftParenthesis);

        List<ParamDeclNode> parameters = [];
        while (tokens.Peek().Type != TokenType.RightParenthesis && tokens.Peek().Type != TokenType.EOF)
        {
            ParamDeclNode? parameter = DoSafe(ParameterDeclaration, ParameterSyncTokens);
            if (parameter is not null)
                parameters.Add(parameter);

            if (tokens.Peek().Type == TokenType.Comma)
                tokens.Next();
            else if (tokens.Peek().Type != TokenType.RightParenthesis)
            {
                if (tokens.Peek().Type is TokenType.Arrow or TokenType.LeftCurlyBrace)
                    break; // heuristic: assume the user forgot to close the parameter list
                
                LogDiagnostic(new ExpectedToken(", or )") { Location = tokens.Peek().Range });
                Synchronize(ParameterSyncTokens);
            }
        }
        
        // don't use Expect here, otherwise we might consume a potential -> or { which worsens error recovery
        if (tokens.Peek().Type != TokenType.RightParenthesis)
            LogDiagnostic(new ExpectedToken(")") { Location = tokens.Peek().Range });
        else
            tokens.Next();
        
        Token? returnType = null;
        if (tokens.Peek().Type == TokenType.Arrow)
        {
            tokens.Next();
            returnType = Expect(TokenType.Identifier);
        }

        BlockNode body = DoSafe(Block, DeclarationSyncTokens) ?? new BlockNode(ReadOnlyCollection<AstNode>.Empty);
        string? returnTypeName = returnType.HasValue ? Fetch(returnType.Value).ToString() : null;
        return new FunctionDeclNode(Fetch(name).ToString(), parameters.AsReadOnly(), returnTypeName, body);
    }

    public ParamDeclNode ParameterDeclaration()
    {
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.Colon);
        Token type = Expect(TokenType.Identifier);

        return new ParamDeclNode(Fetch(name).ToString(), Fetch(type).ToString());
    }

    public BlockNode Block()
    {
        Expect(TokenType.LeftCurlyBrace);

        List<AstNode> statements = [];
        while (tokens.Peek().Type != TokenType.RightCurlyBrace && tokens.Peek().Type != TokenType.EOF)
        {
            AstNode? statement = DoSafe(Statement, StatementSyncTokens);
            if (statement is not null)
                statements.Add(statement);
        }
        
        Expect(TokenType.RightCurlyBrace);

        return new BlockNode(statements.AsReadOnly());
    }

    public ReturnNode Return()
    {
        Expect(TokenType.Return);
        
        // if no expression is provided, return empty
        if (!PrefixParsers.ContainsKey(tokens.Peek().Type))
            return new ReturnNode();
        
        AstNode expression = Do(Expression);
        return new ReturnNode(expression);
    }

    public AstNode Expression() => Expression(0);

    public AstNode Expression(int precedence)
    {
        Token token = tokens.Next();

        if (!PrefixParsers.TryGetValue(token.Type, out var prefixParser))
            ThrowError(new UnexpectedToken(Fetch(token).ToString()) { Location = token.Range });

        AstNode left = Do(() => prefixParser!(this, token));
        
        while (precedence < FetchTokenPrecedence())
        {
            token = tokens.Next();
            left = InfixParsers[token.Type](this, left, token);
        }

        return left;
    }
    
    // Note: Do and DoSafe don't properly set locations if the node is one token long, those node locations are set directly elsewhere
    public T Do<T>(Func<T> parser) where T : AstNode
    {
        var start = tokens.Peek().Range.Start;
        T node = parser();
        var end = tokens.Current.Range.End;
        
        // don't set location if the node is a single token, whose location is set in the prefix parsers
        if (node.Location.Start.Value == 0 && node.Location.End.Value == 0) 
            node.Location = start..end;
        
        return node;
    }
    
    public T? DoSafe<T>(Func<T> parser, HashSet<TokenType> syncTokens) where T : AstNode
    {
        try
        {
            return Do(parser);
        }
        catch (ParseException) // error message already recorded
        {
            Synchronize(syncTokens);
            return null;
        }
    }
    
    private void Synchronize(HashSet<TokenType> syncTokens)
    {
        while (Peek().Type != TokenType.EOF && !syncTokens.Contains(Peek().Type))
            Next();
    }
    
    private Token Peek() => tokens.Peek();
    
    private Token Next() => tokens.Next();

    private ReadOnlyMemory<char> Fetch(Token token) => source[token.Range];

    private int FetchTokenPrecedence() => TokenPrecedences.GetValueOrDefault(Peek().Type, 0);

    private Token Expect(TokenType type)
    {
        Token next = Next();
        if (next.Type != type)
            ThrowError(new ExpectedToken(type.ToSimpleString()) { Location = next.Range });
        
        return next;
    }

    private static AstNode InfixOp(Parser parser, AstNode left, Token token)
    {
        AstNode right = parser.Do(() => parser.Expression(TokenPrecedences[token.Type]));
        return new BinaryOperationNode(token, left, right);
    }
    
    private static string ProcessEscapes(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('\\'))
            return input;
            
        StringBuilder result = new StringBuilder(input.Length);
        
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '\\' && i + 1 < input.Length)
            {
                switch (input[++i])
                {
                    case 'n': result.Append('\n'); break;
                    case 't': result.Append('\t'); break;
                    case 'r': result.Append('\r'); break;
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    default: result.Append('\\').Append(input[i]); break;
                }
            }
            else
                result.Append(input[i]);
        }
        
        return result.ToString();
    }

    private void LogDiagnostic(CompileDiagnostic diagnostic) => _diagnostics.Add(diagnostic);
    
    private AstNode ThrowError(CompileDiagnostic diagnostic)
    {
        LogDiagnostic(diagnostic);
        throw new ParseException();
    }

    private class ParseException : Exception;
}