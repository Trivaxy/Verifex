using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Creates symbols and attaches them to AST nodes (except member accesses & struct initializers), and completes top-level symbols which were gathered earlier
public class FirstBindingPass(SymbolTable symbols) : VerificationPass(symbols)
{
    private int _nextLocalIndex;
    private int _nextParameterIndex;
    private RefinedTypeValueSymbol? _currentValueSymbol;
    private StructSymbol? _currentStruct;

    protected override void Visit(StructDeclNode node)
    {
        _currentStruct = node.Symbol as StructSymbol;
        base.Visit(node);
        _currentStruct = null;
    }

    protected override void Visit(FunctionDeclNode node)
    {
        _nextLocalIndex = 0;
        _nextParameterIndex = 0;
        
        Symbols.EnterNewScope(); // ensures parameters and instance fields are properly scoped

        if (!node.IsStatic && _currentStruct != null)
        {
            foreach (StructFieldSymbol field in _currentStruct!.Fields.Values)
                Symbols.TryAddSymbol(field);
            
            _nextParameterIndex = 1; // skip the first parameter which is the instance
        }

        if (_currentStruct != null)
        {
            foreach (FunctionSymbol method in _currentStruct!.Methods.Values)
                Symbols.TryAddSymbol(method);
        }
        
        base.Visit(node);
        Symbols.ExitScope();
    }

    protected override void Visit(BlockNode node)
    {
        Symbols.EnterNewScope();
        base.Visit(node);
        Symbols.ExitScope();
    }

    protected override void Visit(VarDeclNode node)
    {
        base.Visit(node);

        LocalVarSymbol local = new LocalVarSymbol()
        {
            Name = node.Name,
            DeclaringNode = node,
            IsMutable = node.Mutable,
            IsParameter = false,
            Index = _nextLocalIndex
        };

        if (Symbols.TryLookupSymbol(node.Name, out Symbol? existingSymbol))
        {
            LogDiagnostic(new VarNameAlreadyDeclared(node.Name) { Location = node.Location });
            node.Symbol = existingSymbol; // point to existing symbol, makes things easier for the next passes
        }
        else
        {
            Symbols.TryAddSymbol(local);
            node.Symbol = local;
            _nextLocalIndex++;
        }
    }

    protected override void Visit(ParamDeclNode node)
    {
        base.Visit(node);
        
        LocalVarSymbol parameter = new LocalVarSymbol()
        {
            DeclaringNode = node,
            Name = node.Identifier,
            IsMutable = false,
            IsParameter = true,
            Index = _nextParameterIndex
        };

        if (Symbols.TryLookupSymbol(parameter.Name, out Symbol? existingSymbol))
        {
            LogDiagnostic(new ParameterAlreadyDeclared(parameter.Name) { Location = node.Location });
            node.Symbol = existingSymbol; // point to existing symbol, makes things easier for the next passes
        }
        else
        {
            Symbols.TryAddSymbol(parameter);
            node.Symbol = parameter;
            _nextParameterIndex++;
        }
    }

    protected override void Visit(IdentifierNode node)
    {
        if (_currentValueSymbol != null && node.Identifier == "value")
        {
            node.Symbol = _currentValueSymbol;
            return;
        }
        
        if (Symbols.TryLookupSymbol(node.Identifier, out Symbol? symbol))
            node.Symbol = symbol;
        else
            LogDiagnostic(new UnknownIdentifier(node.Identifier) { Location = node.Location });
    }

    protected override void Visit(RefinedTypeDeclNode node)
    {
        RefinedTypeSymbol refinedTypeSymbol = (node.Symbol as RefinedTypeSymbol)!;
        _currentValueSymbol = new RefinedTypeValueSymbol()
        {
            DeclaringNode = node,
            Name = "value",
            ResolvedType = refinedTypeSymbol.BaseType
        };
        
        refinedTypeSymbol.ValueSymbol = _currentValueSymbol;
        base.Visit(node);

        _currentValueSymbol = null;
    }

    protected override void Visit(InitializerNode node) => Visit(node.InitializerList);
    
    protected override void Visit(InitializerFieldNode node) => Visit(node.Value);

    protected override void Visit(MemberAccessNode node)
    {
        StructSymbol? structSymbol = null;
        bool isStaticAccess = node.Target is IdentifierNode structName && Symbols.TryLookupGlobalSymbol(structName.Identifier, out structSymbol);
        
        if (isStaticAccess)
        {
            if (structSymbol!.Methods.TryGetValue(node.Member.Identifier, out FunctionSymbol? staticMethodSymbol) && staticMethodSymbol.Function.IsStatic)
                node.Symbol = staticMethodSymbol;
        }
        else
            base.Visit(node);
    }
}
