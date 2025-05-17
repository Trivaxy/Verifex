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
    private readonly PersistedAssemblyBuilder _assembly;
    private readonly ModuleBuilder _module;
    private readonly TypeBuilder _type;
    private ILGenerator _il; // for the current method being generated
    private readonly SymbolTable _symbolTable;
    private readonly Dictionary<VerifexFunction, MethodInfo> _methodInfos = new();
    private readonly Dictionary<VerifexFunction, ILGenerator> _methodILGenerators = new();
    private VerifexFunction _currentFunction = null!;

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
        
        foreach (StructSymbol structSymbol in _symbolTable.GetGlobalSymbols<StructSymbol>())
        {
            foreach (FunctionSymbol functionSymbol in structSymbol.Methods.Values)
            {
                VerifexFunction function = functionSymbol.Function;
                if (function is BuiltinFunction builtin)
                {
                    _methodInfos.Add(builtin, builtin.Method);
                    continue;
                }
                
                // check is important otherwise we might define the same method twice, due to embedded structs
                if (_methodInfos.ContainsKey(function)) continue;
                
                MethodBuilder method = _type.DefineMethod(structSymbol.Name + "$" + function.Name, MethodAttributes.Public | MethodAttributes.Static);
                Type returnType = function.ReturnType.IlType;
                IEnumerable<Type> parameterTypes = function.Parameters.Select(p => p.Type.IlType);
                
                if (!function.IsStatic)
                    parameterTypes = new[] { structSymbol.ResolvedType!.IlType }.Concat(parameterTypes);
                
                method.SetReturnType(returnType);
                method.SetParameters(parameterTypes.ToArray());
                
                _il = method.GetILGenerator();
                
                _methodInfos.Add(function, method);
                _methodILGenerators.Add(function, _il);
            }
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
        if (node.FundamentalType is RealType && node.Left.FundamentalType is IntegerType)
            EmitConversion(_symbolTable.GetType("Int"), _symbolTable.GetType("Real"));
        else if (node.FundamentalType is StringType && node.Left.FundamentalType is not StringType)
            EmitConversion(node.Left.FundamentalType!, _symbolTable.GetType("String"));
        
        Visit(node.Right);
        if (node.FundamentalType is RealType && node.Right.FundamentalType is IntegerType)
            EmitConversion(_symbolTable.GetType("Int"), _symbolTable.GetType("Real"));
        else if (node.FundamentalType is StringType && node.Right.FundamentalType is not StringType)
            EmitConversion(node.Right.FundamentalType!, _symbolTable.GetType("String"));

        switch (node.Operator.Type)
        {
            case TokenType.Plus:
                if (node.FundamentalType is StringType)
                    _il.Emit(OpCodes.Call, typeof(string).GetMethod("Concat", [typeof(string), typeof(string)])!);
                else
                    _il.Emit(OpCodes.Add);
                break;
            case TokenType.Minus:
                _il.Emit(OpCodes.Sub);
                break;
            case TokenType.Star:
                _il.Emit(OpCodes.Mul);
                break;
            case TokenType.Slash:
                _il.Emit(OpCodes.Div);
                break;
            case TokenType.GreaterThan:
                _il.Emit(OpCodes.Cgt);
                break;
            case TokenType.LessThan:
                _il.Emit(OpCodes.Clt);
                break;
            case TokenType.GreaterThanOrEqual:
                _il.Emit(OpCodes.Clt);
                _il.Emit(OpCodes.Ldc_I4_0);
                _il.Emit(OpCodes.Ceq);
                break;
            case TokenType.LessThanOrEqual:
                _il.Emit(OpCodes.Cgt);
                _il.Emit(OpCodes.Ldc_I4_0);
                _il.Emit(OpCodes.Ceq);
                break;
            case TokenType.EqualEqual:
                _il.Emit(OpCodes.Ceq);
                break;
            case TokenType.NotEqual:
                _il.Emit(OpCodes.Ceq);
                _il.Emit(OpCodes.Ldc_I4_0);
                _il.Emit(OpCodes.Ceq);
                break;
            case TokenType.And:
                _il.Emit(OpCodes.And);
                break;
            case TokenType.Or:
                _il.Emit(OpCodes.Or);
                break;
            default:
                throw new NotImplementedException($"Unsupported operator {node.Operator.Type}");
        }
    }

    protected override void Visit(BlockNode node)
    {
        foreach (AstNode child in node.Nodes)
        {
            Visit(child);
            
            // special case: if the statement is a function call, we need to pop its unused return off the stack
            if (child is FunctionCallNode { EffectiveType: not null and not VoidType })
                _il.Emit(OpCodes.Pop);
        }
    }

    protected override void Visit(FunctionCallNode node)
    {
        if (node.Callee.Symbol is LocalVarSymbol or FunctionSymbol { Function: { IsStatic: false, Owner: not null } })
            Visit((node.Callee as MemberAccessNode)!.Target);
        
        VerifexFunction function = (node.Callee.Symbol as FunctionSymbol)!.Function;
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            AstNode argument = node.Arguments[i];
            Visit(argument);
            EmitConversion(argument.ResolvedType!, function.Parameters[i].Type);
        }

        _il.Emit(OpCodes.Call, _methodInfos[function]);
    }

    protected override void Visit(FunctionDeclNode node)
    {
        VerifexFunction function = (node.Symbol as FunctionSymbol)!.Function;
        _il = _methodILGenerators[function];
        _currentFunction = function;
        
        Visit(node.Body);
        
        // if the function has a void return type and doesn't end with a return statement, emit a return implicitly
        if (function.ReturnType.IlType == typeof(void) && (node.Body.Nodes.Count == 0 || node.Body.Nodes[^1] is not ReturnNode)) 
            _il.Emit(OpCodes.Ret);
    }

    protected override void Visit(VarDeclNode node)
    {
        Visit(node.Value);
        EmitConversion(node.Value.ResolvedType!, node.ResolvedType!);
        
        LocalVarSymbol local = (node.Symbol as LocalVarSymbol)!;
        _il.DeclareLocal(local.ResolvedType!.IlType);
        _il.Emit(OpCodes.Stloc, (node.Symbol as LocalVarSymbol)!.Index);
    }

    protected override void Visit(IdentifierNode node)
    {
        if (node.Symbol is LocalVarSymbol local)
            _il.Emit(local.IsParameter ? OpCodes.Ldarg : OpCodes.Ldloc, local.Index);
        else if (node.Symbol is StructFieldSymbol field)
        {
            _il.Emit(OpCodes.Ldarg_0);
            _il.Emit(OpCodes.Ldstr, field.Name);
            _il.Emit(OpCodes.Call, typeof(Dictionary<string, object>).GetMethod("get_Item", [typeof(string)])!);
            EmitConversion(_symbolTable.GetType("Any")!, node.ResolvedType!);
        }
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

    protected override void Visit(MinusNegationNode node)
    {
        Visit(node.Operand);
        _il.Emit(OpCodes.Neg);
    }
    
    protected override void Visit(ReturnNode node)
    {
        if (node.Value != null)
        {
            Visit(node.Value);
            EmitConversion(node.Value.ResolvedType!, _currentFunction.ReturnType);
        }

        _il.Emit(OpCodes.Ret);
    }

    protected override void Visit(StringLiteralNode node)
    {
        _il.Emit(OpCodes.Ldstr, node.Value);
    }

    protected override void Visit(BoolLiteralNode node) => _il.Emit(node.Value ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

    protected override void Visit(NotNegationNode node)
    {
        Visit(node.Operand);
        _il.Emit(OpCodes.Ldc_I4_0);
        _il.Emit(OpCodes.Ceq);
    }

    protected override void Visit(IfElseNode node)
    {
        Label elseLabel = _il.DefineLabel();
        Label endLabel = _il.DefineLabel();

        Visit(node.Condition);

        _il.Emit(OpCodes.Brfalse, node.ElseBody != null ? elseLabel : endLabel);
        
        Visit(node.IfBody);

        if (node.ElseBody != null)
            _il.Emit(OpCodes.Br, endLabel);
            
        if (node.ElseBody != null)
        {
            _il.MarkLabel(elseLabel);
            Visit(node.ElseBody);
        }
        
        // Mark the end of the if-else statement
        _il.MarkLabel(endLabel);
    }

    protected override void Visit(AssignmentNode node)
    {
        if (node.Target is IdentifierNode)
        {
            if (node.Target.Symbol is LocalVarSymbol local)
            {
                Visit(node.Value);
                EmitConversion(node.Value.ResolvedType!, node.Target.ResolvedType!);
                _il.Emit(local.IsParameter ? OpCodes.Starg : OpCodes.Stloc, local.Index);
            }
            else if (node.Target.Symbol is StructFieldSymbol field)
            {
                _il.Emit(OpCodes.Ldarg_0);
                _il.Emit(OpCodes.Ldstr, field.Name);
                Visit(node.Value);
                
                EmitConversion(node.Value.ResolvedType!, field.ResolvedType!); // convert to the field type
                EmitConversion(field.ResolvedType!, _symbolTable.GetType("Any")!); // then box
                _il.Emit(OpCodes.Call, typeof(Dictionary<string, object>).GetMethod("set_Item", [typeof(string), typeof(object)])!);
            }
            else
                throw new InvalidOperationException("Identifier node does not have an appropriate symbol");
        }
        else // this is a struct's field
        {
            Visit((node.Target as MemberAccessNode)!.Target);
            _il.Emit(OpCodes.Ldstr, (node.Target as MemberAccessNode)!.Member.Identifier);
            
            Visit(node.Value);
            EmitConversion(node.Value.ResolvedType!, node.Target.ResolvedType!); // convert to the target type
            EmitConversion(node.Target.ResolvedType!, _symbolTable.GetType("Any")); // then box
            
            _il.Emit(OpCodes.Call, typeof(Dictionary<string, object>).GetMethod("set_Item", [typeof(string), typeof(object)])!);
        }
    }

    protected override void Visit(WhileNode node)
    {
        Label startLabel = _il.DefineLabel();
        Label endLabel = _il.DefineLabel();

        _il.MarkLabel(startLabel);
        Visit(node.Condition);
        _il.Emit(OpCodes.Brfalse, endLabel);

        Visit(node.Body);
        _il.Emit(OpCodes.Br, startLabel);
        
        _il.MarkLabel(endLabel);
    }

    protected override void Visit(RefinedTypeDeclNode node)
    {
        // no-op
    }

    protected override void Visit(InitializerNode node)
    {
        _il.Emit(OpCodes.Newobj, typeof(Dictionary<string, object>).GetConstructor(Type.EmptyTypes)!);
        
        foreach (InitializerFieldNode field in node.InitializerList.Values)
        {
            _il.Emit(OpCodes.Dup);
            _il.Emit(OpCodes.Ldstr, field.Name.Identifier);
            Visit(field.Value);
            EmitConversion(field.Value.ResolvedType!, field.Name.ResolvedType!); // convert to the field type
            EmitConversion(field.Name.ResolvedType!, _symbolTable.GetType("Any")); // then box
            _il.Emit(OpCodes.Call, typeof(Dictionary<string, object>).GetMethod("Add", [typeof(string), typeof(object)])!);
        }
    }

    protected override void Visit(MemberAccessNode node)
    {
        // this is a static access to a struct method, don't visit the target
        if (node.Target is IdentifierNode && node.Target.Symbol is FunctionSymbol) return;
        
        Visit(node.Target);
        
        _il.Emit(OpCodes.Ldstr, node.Member.Identifier);
        _il.Emit(OpCodes.Call, typeof(Dictionary<string, object>).GetMethod("get_Item", [typeof(string)])!);
        
        EmitConversion(_symbolTable.GetType("Any"), node.ResolvedType!);
    }

    protected override void Visit(IsCheckNode node)
    {
        Visit(node.Value);
        _il.Emit(OpCodes.Isinst, node.TestedType.EffectiveType!.IlType);
        _il.Emit(OpCodes.Ldnull);
        _il.Emit(OpCodes.Cgt_Un);
    }
    
    private void EmitConversion(VerifexType from, VerifexType to)
    {
        if (from.IlType == to.IlType) return;

        if (from.IlType.IsValueType && to.FundamentalType is AnyType or MaybeType)
            _il.Emit(OpCodes.Box, from.IlType);
        else if (from.FundamentalType is not StringType && to.FundamentalType is StringType)
            _il.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", [from.IlType])!);
        else if (from.FundamentalType is IntegerType && to.FundamentalType is RealType)
            _il.Emit(OpCodes.Conv_R8);
        else if (from.FundamentalType is AnyType or MaybeType)
            _il.Emit(to.IlType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, to.IlType);
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
