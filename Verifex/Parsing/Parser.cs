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
        [TokenType.Let, TokenType.Mut, TokenType.Return, TokenType.Identifier, TokenType.RightCurlyBrace];

    private static readonly HashSet<TokenType> DeclarationSyncTokens = 
        [TokenType.Fn, TokenType.Type, TokenType.Struct, TokenType.EOF];

    private static readonly HashSet<TokenType> ParameterSyncTokens = 
        [TokenType.RightParenthesis, TokenType.Identifier, TokenType.Arrow, TokenType.LeftCurlyBrace];

    private static readonly HashSet<TokenType> StructMemberSyncTokens =
        [TokenType.RightCurlyBrace, TokenType.Identifier, TokenType.Fn, TokenType.FnStatic, TokenType.DotDot];
    
    private static readonly Dictionary<TokenType, Func<Parser, Token, AstNode>> PrefixParsers = new()
    {
        { TokenType.Minus, (parser, _) => new MinusNegationNode(parser.Expression()) },
        { TokenType.Not, (parser, _) => new NotNegationNode(parser.Expression()) },
        { TokenType.Plus, (parser, _) => parser.Expression() },
        { TokenType.Number, (parser, token) => new NumberNode(parser.Fetch(token).ToString()) { Location = token.Range } },
        { TokenType.Identifier, (parser, token) =>
        {
            IdentifierNode identifier = new IdentifierNode(parser.Fetch(token).ToString()) { Location = token.Range };
            if (parser.Peek().Type != TokenType.LeftCurlyBrace) return identifier;
            
            InitializerListNode initializerList = parser.Do(parser.InitializerList);
            return new InitializerNode(identifier, initializerList);

        } },
        { TokenType.Bool, (parser, token) => new BoolLiteralNode(bool.Parse(parser.Fetch(token).ToString())) { Location = token.Range } },
        { TokenType.String, (parser, token) => new StringLiteralNode(ProcessEscapes(parser.Fetch(token).ToString()[1..^1])) { Location = token.Range } },
        { TokenType.LeftParenthesis, (parser, _) =>
        {
            AstNode expression = parser.Do(parser.Expression);
            parser.Expect(TokenType.RightParenthesis);

            return expression;
        }},
        { TokenType.LeftBracket, (parser, token) => // Array Literal
            {
                List<AstNode> elements = [];
                if (parser.Peek().Type != TokenType.RightBracket)
                {
                    elements.Add(parser.Do(parser.Expression));
                    while (parser.Peek().Type == TokenType.Comma)
                    {
                        parser.Next(); // consume comma
                        elements.Add(parser.Do(parser.Expression));
                    }
                }

                parser.Expect(TokenType.RightBracket);
                ArrayLiteralNode arrayLiteralNode = new ArrayLiteralNode(elements.AsReadOnly());
                
                return arrayLiteralNode;
            }
        },
        { TokenType.Hashtag, (parser, token) => new GetLengthNode(parser.Expression()) },
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
        } },
        { TokenType.Dot, (parser, left, _) =>
        {
            Token identifier = parser.Expect(TokenType.Identifier);
            IdentifierNode member = new IdentifierNode(parser.Fetch(identifier).ToString()) { Location = identifier.Range };
            
            return new MemberAccessNode(left, member);
        } },
        { TokenType.Is, (parser, left, _) =>
        {
            AstNode type = parser.Do(parser.TypeName);
            return new IsCheckNode(left, type);
        } },
        { TokenType.LeftBracket, (parser, left, token) =>
        {
            AstNode index = parser.Do(parser.Expression);
            parser.Expect(TokenType.RightBracket);
            IndexAccessNode indexAccessNode = new IndexAccessNode(left, index);
            
            return indexAccessNode;
        } },
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
        { TokenType.Is, 4 },
        { TokenType.Plus, 5 },
        { TokenType.Minus, 5 },
        { TokenType.Star, 6 },
        { TokenType.Slash, 6 },
        { TokenType.LeftParenthesis, 10 },
        { TokenType.Dot, 11 },
        { TokenType.LeftBracket, 11 },
    };

    public ProgramNode Program()
    {
        List<AstNode> nodes = [];
        while (tokens.Peek().Type != TokenType.EOF)
        {
            AstNode? item = DoSafe(Item, DeclarationSyncTokens);
            if (item is not null)
                nodes.Add(item);
        }

        return new ProgramNode(nodes.AsReadOnly());
    }

    public AstNode Item()
    {
        TokenType peekedTokenType = tokens.Peek().Type;

        AstNode item = peekedTokenType switch
        {
            TokenType.Fn => FnDeclaration(),
            TokenType.Type => RefinedTypeDeclaration(),
            TokenType.Struct => StructDeclaration(),
            _ => ThrowError(new UnexpectedToken(Fetch(tokens.Peek()).ToString()) { Location = tokens.Peek().Range })
        };
        
        if (peekedTokenType == TokenType.Type)
            Expect(TokenType.Semicolon);

        return item;
    }

    public AstNode Statement()
    {
        TokenType peekedTokenType = tokens.Peek().Type;
        
        AstNode? statement = Do<AstNode>(peekedTokenType switch
        {
            TokenType.Let => LetDeclaration,
            TokenType.Mut => MutDeclaration,
            TokenType.Return => Return,
            TokenType.If => IfStatement,
            TokenType.While => WhileStatement,
            _ => () =>
            {
                if (PrefixParsers.ContainsKey(peekedTokenType))
                {
                    AstNode target = Do(Expression);
                    
                    if (target is FunctionCallNode && !InfixParsers.ContainsKey(tokens.Peek().Type))
                        return target;
                    
                    Expect(TokenType.Equals);
                    AstNode value = Do(Expression);
                    
                    return new AssignmentNode(target, value);
                }

                return ThrowError(new UnexpectedToken(Fetch(tokens.Peek()).ToString()) { Location = tokens.Peek().Range });
            }
        });

        // Don't expect a semicolon for if/while statements - they end with blocks
        if (peekedTokenType is not TokenType.If and not TokenType.While)
            Expect(TokenType.Semicolon);
        
        return statement;
    }

    public VarDeclNode LetDeclaration()
    {
        Expect(TokenType.Let);
        Token name = Expect(TokenType.Identifier);
        AstNode? typeHint = null;
        
        if (tokens.Peek().Type == TokenType.Colon)
        {
            tokens.Next(); // consume colon
            typeHint = Do(TypeName);
        }
        
        Expect(TokenType.Equals);
        AstNode value = Do(Expression);

        return new VarDeclNode(Fetch(name).ToString(), typeHint, value);
    }

    public VarDeclNode MutDeclaration()
    {
        Expect(TokenType.Mut);
        Token name = Expect(TokenType.Identifier);
        AstNode? typeHint = null;
        
        if (tokens.Peek().Type == TokenType.Colon)
        {
            tokens.Next(); // consume colon
            typeHint = Do(TypeName);
        }
        
        Expect(TokenType.Equals);
        AstNode value = Do(Expression);

        return new VarDeclNode(Fetch(name).ToString(), typeHint, value, true);
    }

    public FunctionDeclNode FnDeclaration()
    { 
        bool isStatic = ExpectEither(TokenType.Fn, TokenType.FnStatic).Type == TokenType.FnStatic;
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
        
        AstNode? returnType = null;
        if (tokens.Peek().Type == TokenType.Arrow)
        {
            tokens.Next();
            returnType = Do(TypeName);
        }

        BlockNode body = DoSafe(Block, DeclarationSyncTokens) ?? new BlockNode(ReadOnlyCollection<AstNode>.Empty);
        return new FunctionDeclNode(Fetch(name).ToString(), isStatic, parameters.AsReadOnly(), returnType, body);
    }

    public ParamDeclNode ParameterDeclaration()
    {
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.Colon);
        AstNode type = Do(TypeName);

        return new ParamDeclNode(Fetch(name).ToString(), type);
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

    public IfElseNode IfStatement()
    {
        Expect(TokenType.If);
        Expect(TokenType.LeftParenthesis);
        
        AstNode condition = Do(Expression);
        
        Expect(TokenType.RightParenthesis);
        
        BlockNode ifBody = Do(Block);
        
        BlockNode? elseBody = null;
        if (tokens.Peek().Type == TokenType.Else)
        {
            tokens.Next(); // consume the else
            
            // check if this is an "else if"
            if (tokens.Peek().Type == TokenType.If)
            {
                IfElseNode elseIfNode = DoSafe(IfStatement, StatementSyncTokens) ??
                                        new IfElseNode(
                                            new BoolLiteralNode(false),
                                            new BlockNode(ReadOnlyCollection<AstNode>.Empty)
                                        );
                
                // turn else-if into else { if... }
                elseBody = new BlockNode(new ReadOnlyCollection<AstNode>(new[] { elseIfNode }));
            }
            else
                elseBody = Do(Block);
        }
        
        return new IfElseNode(condition, ifBody, elseBody);
    }

    public WhileNode WhileStatement()
    {
        Expect(TokenType.While);
        Expect(TokenType.LeftParenthesis);
        AstNode condition = Do(Expression);
        Expect(TokenType.RightParenthesis);
        BlockNode body = Do(Block);
        
        return new WhileNode(condition, body);
    }

    public RefinedTypeDeclNode RefinedTypeDeclaration()
    {
        Expect(TokenType.Type);
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.Equals);
        AstNode baseType = Do(TypeName);
        Expect(TokenType.Where);
        AstNode expression = Do(Expression);

        return new RefinedTypeDeclNode(Fetch(name).ToString(), baseType, expression);
    }

    public StructDeclNode StructDeclaration()
    {
        Expect(TokenType.Struct);
        Token name = Expect(TokenType.Identifier);
        Expect(TokenType.LeftCurlyBrace);
        
        List<StructFieldNode> fields = [];
        List<StructMethodNode> methods = [];
        List<IdentifierNode> embedded = [];
        while (tokens.Peek().Type != TokenType.RightCurlyBrace && tokens.Peek().Type != TokenType.EOF)
        {
            if (tokens.Peek().Type is TokenType.Fn or TokenType.FnStatic)
            {
                FunctionDeclNode? method = DoSafe(FnDeclaration, DeclarationSyncTokens);
                if (method is not null)
                    methods.Add(new StructMethodNode(method));
            }
            else if (tokens.Peek().Type is TokenType.DotDot)
            {
                IdentifierNode? embeddedStruct = DoSafe(EmbeddedStruct, StructMemberSyncTokens);
                if (embeddedStruct is not null)
                    embedded.Add(embeddedStruct);
                
                if (tokens.Peek().Type == TokenType.Comma)
                    tokens.Next(); // consume the comma
                else if (tokens.Peek().Type != TokenType.RightCurlyBrace)
                {
                    LogDiagnostic(new ExpectedToken(", or }") { Location = tokens.Peek().Range });
                    Synchronize(StructMemberSyncTokens);
                }
            }
            else
            {
                StructFieldNode? field = DoSafe(StructField, StructMemberSyncTokens);

                if (field is not null)
                    fields.Add(field);

                if (tokens.Peek().Type == TokenType.Comma)
                    tokens.Next(); // consume the comma
                else if (tokens.Peek().Type != TokenType.RightCurlyBrace)
                {
                    LogDiagnostic(new ExpectedToken(", or }") { Location = tokens.Peek().Range });
                    Synchronize(StructMemberSyncTokens);
                }
            }
        }
        
        Expect(TokenType.RightCurlyBrace);
        
        return new StructDeclNode(Fetch(name).ToString(), fields.AsReadOnly(), methods.AsReadOnly(), embedded.AsReadOnly());
    }

    public StructFieldNode StructField()
    {
        Token fieldName = Expect(TokenType.Identifier);
        Expect(TokenType.Colon);
        AstNode fieldType = Do(TypeName);
        
        return new StructFieldNode(Fetch(fieldName).ToString(), fieldType);
    }

    public IdentifierNode EmbeddedStruct()
    {
        Expect(TokenType.DotDot);
        
        Token structName = Expect(TokenType.Identifier);
        return new IdentifierNode(Fetch(structName).ToString()) { Location = structName.Range };
    }

    public InitializerListNode InitializerList()
    {
        Expect(TokenType.LeftCurlyBrace);
        
        List<InitializerFieldNode> values = [];
        while (tokens.Peek().Type != TokenType.RightCurlyBrace && tokens.Peek().Type != TokenType.EOF)
        {
            InitializerFieldNode? field = DoSafe(InitializerField, StructMemberSyncTokens);
            if (field is not null)
                values.Add(field);
            
            if (tokens.Peek().Type == TokenType.Comma)
                tokens.Next(); // consume the comma
            else if (tokens.Peek().Type != TokenType.RightCurlyBrace)
            {
                LogDiagnostic(new ExpectedToken(", or }") { Location = tokens.Peek().Range });
                Synchronize(StructMemberSyncTokens);
            }
        }
        
        Expect(TokenType.RightCurlyBrace);
        return new InitializerListNode(values.AsReadOnly());
    }

    public InitializerFieldNode InitializerField()
    {
        IdentifierNode name = Do(Identifier);
        Expect(TokenType.Colon);
        AstNode value = Do(Expression);
        
        return new InitializerFieldNode(name, value);
    }

    public IdentifierNode Identifier()
    {
        Token identifier = Expect(TokenType.Identifier);
        return new IdentifierNode(Fetch(identifier).ToString()) { Location = identifier.Range };
    }

    public AstNode SingleType()
    {
        Token identifier = Expect(TokenType.Identifier);
        AstNode type = new SimpleTypeNode(Fetch(identifier).ToString()) { Location = identifier.Range };

        while (tokens.Peek().Type == TokenType.LeftBracket)
        {
            tokens.Next();
            Expect(TokenType.RightBracket);
            type = new ArrayTypeNode(type);
        }

        return type;
    }

    public AstNode TypeName()
    {
        List<AstNode> types = [Do(SingleType)];

        while (tokens.Peek().Type == TokenType.OrKeyword)
        {
            tokens.Next();
            types.Add(Do(SingleType));
        }

        if (types.Count == 1) return types[0];
        return new MaybeTypeNode(types.AsReadOnly());
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
        
        // don't set location if the node is a single token, those set location themselves
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

    private Token ExpectEither(TokenType first, TokenType second)
    {
        Token next = Next();
        if (next.Type != first && next.Type != second)
            ThrowError(new ExpectedToken($"{first.ToSimpleString()} or {second.ToSimpleString()}") { Location = next.Range });
        
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


