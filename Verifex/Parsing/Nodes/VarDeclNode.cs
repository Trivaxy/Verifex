namespace Verifex.Parsing.Nodes;

public class VarDeclNode(string name, string? typeHint, AstNode value) : AstNode
{
    public readonly string Name = name;
    public readonly string? TypeHint = typeHint;
    public readonly AstNode Value = value;
}