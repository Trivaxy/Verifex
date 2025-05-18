using System.Collections.ObjectModel;
using Verifex.Analysis;
using Verifex.CodeGen.Types;

namespace Verifex.Parsing;

public abstract class AstNode
{
    public Range Location { get; set; }
    
    public Symbol? Symbol { get; set; } // set during binding

    private VerifexType _explicitType = VerifexType.Unknown;
    public VerifexType ResolvedType
    {
        get => _explicitType != VerifexType.Unknown ? _explicitType : (Symbol?.ResolvedType ?? VerifexType.Unknown);
        set => _explicitType = value;
    }
    
    public VerifexType EffectiveType => ResolvedType.EffectiveType;
    
    public VerifexType FundamentalType => ResolvedType.FundamentalType;
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

public class FunctionDeclNode(string name, bool isStatic, ReadOnlyCollection<ParamDeclNode> parameters, AstNode? returnType, BlockNode body) : AstNode
{
    public readonly string Name = name;
    public readonly bool IsStatic = isStatic;
    public readonly ReadOnlyCollection<ParamDeclNode> Parameters = parameters;
    public readonly AstNode? ReturnType = returnType;
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

public class ParamDeclNode(string name, AstNode type) : IdentifierNode(name)
{
    public readonly AstNode Type = type;
}

public class MinusNegationNode(AstNode operand) : AstNode
{
    public readonly AstNode Operand = operand;
}

public class VarDeclNode(string name, AstNode? typeHint, AstNode value, bool mutable = false) : AstNode
{
    public readonly string Name = name;
    public readonly AstNode? TypeHint = typeHint;
    public readonly AstNode Value = value;
    public readonly bool Mutable = mutable;
}

public class BoolLiteralNode(bool value) : AstNode
{
    public readonly bool Value = value;
}

public class NotNegationNode(AstNode operand) : AstNode
{
    public readonly AstNode Operand = operand;
}

public class IfElseNode(AstNode condition, BlockNode ifBody, BlockNode? elseBody = null) : AstNode
{
    public readonly AstNode Condition = condition;
    public readonly BlockNode IfBody = ifBody;
    public readonly BlockNode? ElseBody = elseBody;
}

public class AssignmentNode(AstNode target, AstNode value) : AstNode
{
    public readonly AstNode Target = target;
    public readonly AstNode Value = value;
}

public class WhileNode(AstNode condition, BlockNode body) : AstNode
{
    public readonly AstNode Condition = condition;
    public readonly BlockNode Body = body;
}

public class RefinedTypeDeclNode(string name, AstNode baseType, AstNode expression) : AstNode
{
    public readonly string Name = name;
    public readonly AstNode BaseType = baseType;
    public readonly AstNode Expression = expression;
}

public class StructFieldNode(string name, AstNode type) : AstNode
{
    public readonly string Name = name;
    public readonly AstNode Type = type;
}

public class StructMethodNode(FunctionDeclNode function) : AstNode
{
    public readonly FunctionDeclNode Function = function;
}

public class StructDeclNode(string name, ReadOnlyCollection<StructFieldNode> fields, ReadOnlyCollection<StructMethodNode> methods, ReadOnlyCollection<IdentifierNode> embedded) : AstNode
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<StructFieldNode> Fields = fields;
    public readonly ReadOnlyCollection<StructMethodNode> Methods = methods;
    public readonly ReadOnlyCollection<IdentifierNode> Embedded = embedded;
}

public class InitializerFieldNode(IdentifierNode name, AstNode value) : AstNode
{
    public readonly IdentifierNode Name = name;
    public readonly AstNode Value = value;
}

public class InitializerListNode(ReadOnlyCollection<InitializerFieldNode> values) : AstNode
{
    public readonly ReadOnlyCollection<InitializerFieldNode> Values = values;
}

public class InitializerNode(IdentifierNode type, InitializerListNode initializerList) : AstNode
{
    public readonly IdentifierNode Type = type; // TODO: Turn to SimpleTypeNode
    public readonly InitializerListNode InitializerList = initializerList;
}

public class MemberAccessNode(AstNode target, IdentifierNode member) : AstNode
{
    public readonly AstNode Target = target;
    public readonly IdentifierNode Member = member;
}

public class SimpleTypeNode(string identifier) : IdentifierNode(identifier);

public class MaybeTypeNode(ReadOnlyCollection<SimpleTypeNode> types) : AstNode
{
    public readonly ReadOnlyCollection<SimpleTypeNode> Types = types;
}

public class IsCheckNode(AstNode value, SimpleTypeNode testedType) : AstNode
{
    public readonly AstNode Value = value;
    public readonly SimpleTypeNode TestedType = testedType;
}