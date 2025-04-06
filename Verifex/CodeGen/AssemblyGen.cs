using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using Verifex.Analysis;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.CodeGen;

public class AssemblyGen : DefaultNodeVisitor
{
    private PersistedAssemblyBuilder _assembly;
    private ModuleBuilder _module;
    private TypeBuilder _type;
    private ILGenerator _il; // for the current method being generated
    private SymbolTable _symbolTable;
    private Dictionary<VerifexFunction, MethodInfo> _methodInfos = new();
    private Dictionary<VerifexFunction, ILGenerator> _methodILGenerators = new();

    public AssemblyGen(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
        
        var assemblyName = new AssemblyName("TestProgram");
        _assembly = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
        
        CustomAttributeBuilder targetFramework = new CustomAttributeBuilder(
            typeof(TargetFrameworkAttribute).GetConstructor([typeof(string)])!,
            [".NETCoreApp,Version=v9.0"]);
        
        _assembly.SetCustomAttribute(targetFramework);
                
        _module = _assembly.DefineDynamicModule("Test");
        _type = _module.DefineType("Program", TypeAttributes.Public);
        SetupMethodBuilders();
    }

    private void SetupMethodBuilders()
    {
        foreach (FunctionSymbol functionSymbol in _symbolTable.GetGlobalSymbols<FunctionSymbol>())
        {
            VerifexFunction function = functionSymbol.Function;
            if (function is BuiltinFunction builtin)
            {
                _methodInfos.Add(builtin, builtin.Method);
                continue;
            }
            
            MethodBuilder method = _type.DefineMethod(function.Name, MethodAttributes.Public | MethodAttributes.Static);
            Type returnType = function.ReturnType.IlType;
            Type[] parameterTypes = new Type[function.Parameters.Count];
            for (int i = 0; i < function.Parameters.Count; i++)
            {
                ParameterInfo parameter = function.Parameters[i];
                parameterTypes[i] = parameter.Type.IlType;
            }
            
            method.SetReturnType(returnType);
            method.SetParameters(parameterTypes);
            
            _il = method.GetILGenerator();
            
            _methodInfos.Add(function, method);
            _methodILGenerators.Add(function, _il);
        }
    }
    
    public void Consume(ProgramNode program) => Visit(program);

    protected override void Visit(ProgramNode program)
    {
        foreach (AstNode node in program.Nodes)
            Visit(node);
    }

    protected override void Visit(BinaryOperationNode node)
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

    protected override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
            Visit(child);
    }

    protected override void Visit(FunctionCallNode node)
    {
        VerifexFunction function = _symbolTable.GetGlobalSymbol<FunctionSymbol>((node.Callee as IdentifierNode)!.Identifier).Function;
        foreach (AstNode argument in node.Arguments)
            Visit(argument);
        
        _il.Emit(OpCodes.Call, _methodInfos[function]);
    }

    protected override void Visit(FunctionDeclNode node)
    {
        VerifexFunction function = (node.Symbol as FunctionSymbol)!.Function;
        _il = _methodILGenerators[function];
        
        Visit(node.Body);
        
        // if the function has a void return type and doesn't end with a return statement, emit a return implicitly
        if (function.ReturnType.IlType == typeof(void) && (node.Body.Nodes.Count == 0 || node.Body.Nodes[^1] is not ReturnNode)) 
            _il.Emit(OpCodes.Ret);
    }

    protected override void Visit(VarDeclNode node)
    {
        Visit(node.Value);
        
        LocalVarSymbol local = (node.Symbol as LocalVarSymbol)!;
        _il.DeclareLocal(local.ResolvedType!.IlType);
        _il.Emit(OpCodes.Stloc, (node.Symbol as LocalVarSymbol)!.Index);
    }

    protected override void Visit(IdentifierNode node)
    {
        if (node.Symbol is LocalVarSymbol local)
            _il.Emit(local.IsParameter ? OpCodes.Ldarg : OpCodes.Ldloc, local.Index);
        else
            throw new InvalidOperationException("Identifier node does not have an appropriate symbol");
    }

    protected override void Visit(NumberNode node)
    {
        if (node.NumberType == NumberType.Integer)
            _il.Emit(OpCodes.Ldc_I4, int.Parse(node.Value));
        else
            _il.Emit(OpCodes.Ldc_R8, double.Parse(node.Value));
    }

    protected override void Visit(UnaryNegationNode node)
    {
        Visit(node.Operand);
        _il.Emit(OpCodes.Neg);
    }
    
    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
            Visit(node.Value);
        
        _il.Emit(OpCodes.Ret);
    }

    protected override void Visit(StringLiteralNode node)
    {
        throw new NotImplementedException();
    }

    public void Save(string path)
    {
        CreateEntryPoint();
        _type.CreateType();
        _module.CreateGlobalFunctions();
        SaveExecutableImage(path);
    }

    private void CreateEntryPoint()
    {
        MethodBuilder entryPoint = _type.DefineMethod(
            "Main",
            MethodAttributes.HideBySig | MethodAttributes.Public | MethodAttributes.Static,
            CallingConventions.Standard,
            typeof(void),
            Type.EmptyTypes);
        
        ILGenerator il = entryPoint.GetILGenerator();

        if (_symbolTable.TryLookupGlobalSymbol("main", out FunctionSymbol? function))
        {
            VerifexFunction verifexMainFunction = function.Function;
            il.Emit(OpCodes.Call, _methodInfos[verifexMainFunction]);
        }
        
        il.Emit(OpCodes.Ret);
    }
    
    private void SaveExecutableImage(string assemblyFileName)
    {
        MetadataBuilder metadata = _assembly.GenerateMetadata(out BlobBuilder ilStream, out BlobBuilder mappedFieldData);
        
        ManagedPEBuilder peBuilder = new ManagedPEBuilder(
            header: PEHeaderBuilder.CreateExecutableHeader(),
            metadataRootBuilder: new MetadataRootBuilder(metadata),
            ilStream: ilStream,
            mappedFieldData: mappedFieldData,
            entryPoint: MetadataTokens.MethodDefinitionHandle(_type.GetMethod("Main").MetadataToken)
        );
        
        BlobBuilder peBlob = new BlobBuilder();
        peBuilder.Serialize(peBlob);
        
        using FileStream stream = new FileStream(assemblyFileName, FileMode.Create, FileAccess.Write);
        peBlob.WriteContentTo(stream);
    }
}
