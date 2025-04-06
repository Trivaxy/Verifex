using Verifex.Parsing;

namespace Verifex.Analysis.Pass;

// Creates symbols and attaches them to AST nodes, and completes function symbols
public class BindingPass(SymbolTable symbols) : VerificationPass(symbols)
{
    private int _nextLocalIndex;
    private int _nextParameterIndex;

    protected override void Visit(FunctionDeclNode node)
    {
        base.Visit(node);

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
            IsMutable = false,
            IsParameter = false,
            Index = _nextLocalIndex
        };

        if (!Symbols.TryAddSymbol(local))
            ErrorAt(node, $"{node.Name} already declared in this scope");
        else
        {
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
            ErrorAt(node, $"parameter {parameter.Name} already declared");
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
            ErrorAt(node, $"unknown variable, function, or type {node.Identifier}");
    }
}