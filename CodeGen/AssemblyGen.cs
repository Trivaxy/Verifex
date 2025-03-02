using System.Reflection;
using System.Reflection.Emit;
using Verifex.Analysis;
using Verifex.Analysis.Symbols;
using Verifex.CodeGen.Types;
using Verifex.Parser;
using Verifex.Parser.Nodes;

namespace Verifex.CodeGen;

public class AssemblyGen : NodeVisitor
{
    private PersistedAssemblyBuilder _assembly;
    private ModuleBuilder _module;
    private TypeBuilder _type;
    private ILGenerator _il;
    private Symbols _symbols;

    public override void Visit(ProgramNode program)
    {
        _assembly = new PersistedAssemblyBuilder(new AssemblyName("TestProgram"), typeof(object).Assembly);
        _module = _assembly.DefineDynamicModule("Test");
        _type = _module.DefineType("Main", TypeAttributes.Public);
        _symbols = new Symbols();
        
        foreach (AstNode node in program.Nodes)
            Visit(node);
        
        _type.CreateType();
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
        throw new NotImplementedException();
    }

    public override void Visit(FunctionDeclNode node)
    {
        MethodBuilder method = _type.DefineMethod(node.Name, MethodAttributes.Public | MethodAttributes.Static);
        _il = method.GetILGenerator();
        _symbols.ClearLocals();
        
        Visit(node.Body);
        _il.Emit(OpCodes.Ret);
    }

    public override void Visit(IdentifierNode node)
    {
        // TODO: Use shorter opcodes
        ValueLocation value = _symbols.GetLocal(node.Identifier);
        _il.Emit(OpCodes.Ldloc, value.Index);
    }

    public override void Visit(NumberNode node)
    {
        if (node.NumberType == NumberType.Integer)
            _il.Emit(OpCodes.Ldc_I4, int.Parse(node.Value));
        else
            _il.Emit(OpCodes.Ldc_R8, double.Parse(node.Value));
    }

    public override void Visit(TypedIdentifierNode node)
    {
        throw new NotImplementedException();
    }

    public override void Visit(UnaryNegationNode node)
    {
        Visit(node.Operand);
        _il.Emit(OpCodes.Neg);
    }

    public override void Visit(VarDeclNode node)
    {
        VerifexType type = node.Type != null ? _symbols.GetType(node.Type) : new IntegerType();
        ValueLocation value = _symbols.AddLocal(node.Name, type);
        
        _il.DeclareLocal(value.Type.IlType);
        Visit(node.Value);
        _il.Emit(OpCodes.Stloc, value.Index);
    }

    public void Save(string path)
    {
        _assembly.Save(path);
    }
}