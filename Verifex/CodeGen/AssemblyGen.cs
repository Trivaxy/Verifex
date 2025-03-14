using System.Reflection;
using System.Reflection.Emit;
using Verifex.Analysis;
using Verifex.Analysis.Symbols;
using Verifex.CodeGen.Types;
using Verifex.Parsing;
using Verifex.Parsing.Nodes;

namespace Verifex.CodeGen;

public class AssemblyGen : NodeVisitor
{
    private PersistedAssemblyBuilder _assembly;
    private ModuleBuilder _module;
    private TypeBuilder _type;
    private ILGenerator _il; // for the current method being generated
    private Symbols _symbols;
    private Dictionary<VerifexFunction, MethodInfo> _methodInfos = new Dictionary<VerifexFunction, MethodInfo>();
    private Dictionary<VerifexFunction, ILGenerator> _methodILGenerators = new Dictionary<VerifexFunction, ILGenerator>();

    public AssemblyGen(Symbols symbols)
    {
        _symbols = symbols;
        _assembly = new PersistedAssemblyBuilder(new AssemblyName("TestProgram"), typeof(object).Assembly);
        _module = _assembly.DefineDynamicModule("Test");
        _type = _module.DefineType("Main", TypeAttributes.Public);

        foreach (VerifexFunction function in _symbols.Functions)
        {
            if (function.IsBuiltin) continue;
            
            MethodBuilder method = _type.DefineMethod(function.Name, MethodAttributes.Public | MethodAttributes.Static);
            _il = method.GetILGenerator();
            
            _methodInfos.Add(function, method);
            _methodILGenerators.Add(function, _il);
        }
    }

    public override void Visit(ProgramNode program)
    {
        foreach (AstNode node in program.Nodes)
            Visit(node);
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
        _symbols.PushScope();
        
        foreach (AstNode child in node.Nodes)
            Visit(child);
        
        _symbols.PopScope();
    }

    public override void Visit(FunctionCallNode node)
    {
        IdentifierNode identifier = (IdentifierNode)node.Callee;
        VerifexFunction function = _symbols.GetFunction(identifier.Identifier);
        
        // the function's name in IL is the same as in Verifex
        // assume they all live in the same module and class
        MethodInfo method = _type.GetMethod(function.Name);
    }

    public override void Visit(FunctionDeclNode node)
    {
        VerifexFunction function = _symbols.GetFunction(node.Name);
        _il = _methodILGenerators[function];
        
        _symbols.PushScope();
        Visit(node.Body);
        _il.Emit(OpCodes.Ret);
        _symbols.PopScope();
    }

    public override void Visit(IdentifierNode node)
    {
        // TODO: Use shorter opcodes
        ValueLocation? value = _symbols.GetLocal(node.Identifier);
        
        if (!value.HasValue)
            throw new Exception($"Variable {node.Identifier} has no ValueLocation");

        _il.Emit(OpCodes.Ldloc, value.Value.Index);
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
        ValueLocation value = _symbols.AddLocalToCurrentScope(node.Name, type);
        
        _il.DeclareLocal(value.Type.IlType);
        Visit(node.Value);
        _il.Emit(OpCodes.Stloc, value.Index);
    }

    public void Save(string path)
    {
        _type.CreateType();
        _assembly.Save(path);
    }
}