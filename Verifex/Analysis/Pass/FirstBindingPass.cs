using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Creates symbols and attaches them to AST nodes (except member accesses & struct initializers), and completes top-level symbols which were gathered earlier
public class FirstBindingPass(SymbolTable symbols) : VerificationPass(symbols)
{
    private int _nextLocalIndex;
    private int _nextParameterIndex;
    private RefinedTypeValueSymbol? _currentValueSymbol;

    protected override void Visit(FunctionDeclNode node)
    {
        Symbols.EnterNewScope(); // ensures parameters are properly scoped
        base.Visit(node);
        Symbols.ExitScope();

        _nextLocalIndex = 0;
        _nextParameterIndex = 0;
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
}
