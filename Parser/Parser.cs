using Verifex.Parser.Nodes;

namespace Verifex.Parser;

public class Parser(TokenStream tokens)
{
    private static readonly Dictionary<TokenType, Func<Parser, Token, AstNode>> PrefixParsers = new()
    {
        { TokenType.Minus, (parser, token) => new UnaryNegationNode(parser.Expression(0)) },
        { TokenType.Plus, (parser, token) => parser.Expression(0) },
        { TokenType.Number, (parser, token) => new NumberNode(int.Parse(token.Text.Span)) },
        { TokenType.Identifier, (parser, token) => new IdentifierNode(token.Text.ToString()) },
    };

    private static readonly Dictionary<TokenType, Func<Parser, AstNode, Token, AstNode>> InfixParsers = new()
    {
        { TokenType.Plus, InfixOp },
        { TokenType.Minus, InfixOp },
        { TokenType.Star, InfixOp },
        { TokenType.Slash, InfixOp }
    };

    private static readonly Dictionary<TokenType, int> TokenPrecedences = new()
    {
        { TokenType.Plus, 2 },
        { TokenType.Minus, 2 },
        { TokenType.Star, 3 },
        { TokenType.Slash, 3 }
    };

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

    private int FetchTokenPrecedence() => TokenPrecedences.GetValueOrDefault(tokens.Peek().Type, 0);

    private static AstNode InfixOp(Parser parser, AstNode left, Token token)
    {
        AstNode right = parser.Expression(TokenPrecedences[token.Type]);
        return new BinaryOperationNode(token, left, right);
    }
}