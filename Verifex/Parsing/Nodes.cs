using System.Collections.ObjectModel;
using Verifex.CodeGen.Types;

namespace Verifex.Parsing;

public abstract class AstNode
{
    public Range Location { get; set; }
    
    public VerifexType? Type { get; set; } // Set by the type checker
}

public class BinaryOperationNode(Token operatorToken, AstNode left, AstNode right) : AstNode
{
    public readonly Token Operator = operatorToken;
    public readonly AstNode Left = left;
    public readonly AstNode Right = right;
}

public class BlockNode(ReadOnlyCollection<AstNode> nodes) : AstNode
{
    public readonly ReadOnlyCollection<AstNode> Nodes = nodes;
}

public class FunctionCallNode(AstNode callee, ReadOnlyCollection<AstNode> arguments) : AstNode
{
    public readonly AstNode Callee = callee;
    public readonly ReadOnlyCollection<AstNode> Arguments = arguments;
}

public class FunctionDeclNode(string name, ReadOnlyCollection<TypedIdentifierNode> parameters, string? returnType, BlockNode body) : AstNode
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<TypedIdentifierNode> Parameters = parameters;
    public readonly string? ReturnType = returnType;
    public readonly BlockNode Body = body;
}

public class IdentifierNode(string identifier) : AstNode
{
    public readonly string Identifier = identifier;
}

public class NumberNode(string value) : AstNode
{
    public readonly string Value = value;
    public readonly NumberType NumberType = value.Contains('.') ? NumberType.Real : NumberType.Integer;

    public int AsInteger() => int.Parse(Value);

    public double AsDouble() => double.Parse(Value);
}

public enum NumberType
{
    Integer,
    Real
}

public class ProgramNode(ReadOnlyCollection<AstNode> nodes) : AstNode
{
    public readonly ReadOnlyCollection<AstNode> Nodes = nodes;
}

public class ReturnNode(AstNode? value = null) : AstNode
{
    public readonly AstNode? Value = value;
}

public class StringLiteralNode(string value) : AstNode
{
    public readonly string Value = value;
}

public class TypedIdentifierNode(string name, string type) : IdentifierNode(name)
{
    public readonly string TypeName = type;
}

public class UnaryNegationNode(AstNode operand) : AstNode
{
    public readonly AstNode Operand = operand;
}

public class VarDeclNode(string name, string? typeHint, AstNode value) : AstNode
{
    public readonly string Name = name;
    public readonly string? TypeHint = typeHint;
    public readonly AstNode Value = value;
}