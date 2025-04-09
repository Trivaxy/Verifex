using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Creates symbols and attaches them to AST nodes, and completes function symbols
public class BindingPass(SymbolTable symbols) : VerificationPass(symbols)
{
    private int _nextLocalIndex;
    private int _nextParameterIndex;

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

        if (!Symbols.TryAddSymbol(parameter))
            LogDiagnostic(new ParameterAlreadyDeclared(parameter.Name) { Location = node.Location });
        else
        {
            node.Symbol = parameter;
            _nextParameterIndex++;
        }
    }

    protected override void Visit(IdentifierNode node)
    {
        if (Symbols.TryLookupSymbol(node.Identifier, out Symbol? symbol))
            node.Symbol = symbol;
        else
            LogDiagnostic(new UnknownIdentifier(node.Identifier) { Location = node.Location });
    }
}
