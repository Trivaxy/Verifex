using System.Reflection;
using System.Reflection.Emit;
using Verifex.Analysis;
using Verifex.Analysis.Symbols;
using Verifex.Parser;
using Verifex.Parser.Nodes;

namespace Verifex.CodeGen;

public class AssemblyGen : NodeVisitor
{
    private AssemblyBuilder _assembly;
    private ModuleBuilder _module;
    private TypeBuilder _type;
    private ILGenerator _il;
    private Symbols _symbols;

    public AssemblyGen()
    {
        _assembly = new PersistedAssemblyBuilder(new AssemblyName("TestProgram"), typeof(object).Assembly);
        _module = _assembly.DefineDynamicModule("Test");
        _type = _module.DefineType("Main", TypeAttributes.Public);
        _symbols = new Symbols();
    }

    public override void Visit(BinaryOperationNode node)
    {
        Visit(node.Left);
        Visit(node.Right);
        
        _il.Emit(node.Operator.Type switch
        {
            TokenType.Plus => OpCodes.Add,
            TokenType.Minus => OpCodes.Sub,
            TokenType.Star => OpCodes.Mul,
            TokenType.Slash => OpCodes.Div,
            _ => throw new NotImplementedException(),
        });
    }

    public override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    public override void Visit(FunctionCallNode node)
    {
    }

    public override void Visit(FunctionDeclNode node)
    {
    }

    public override void Visit(IdentifierNode node)
    {
        base.Visit(node);
    }

    public override void Visit(NumberNode node)
    {
        base.Visit(node);
    }

    public override void Visit(TypedIdentifierNode node)
    {
        base.Visit(node);
    }

    public override void Visit(UnaryNegationNode node)
    {
        base.Visit(node);
    }

    public override void Visit(VarDeclNode node)
    {
        base.Visit(node);
    }
}