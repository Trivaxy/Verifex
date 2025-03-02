namespace Verifex.Parser.Nodes;

public class VarDeclNode(string name, string? type, AstNode value) : AstNode
{
    public readonly string Name = name;
    public readonly string? Type = type;
    public readonly AstNode Value = value;
}