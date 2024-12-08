namespace Verifex.Parser.Nodes;

public class VarDeclNode(string name, AstNode value) : AstNode
{
    public readonly string Name = name;
    public readonly AstNode Value = value;
}