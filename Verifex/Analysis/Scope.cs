using System.Diagnostics.CodeAnalysis;
using Verifex.CodeGen.Types;

namespace Verifex.Analysis;

public class SymbolTable
{
    public Scope GlobalScope { get; init; }

    public Scope CurrentScope { get; private set; }

    public SymbolTable()
    {
        GlobalScope = new Scope();
        CurrentScope = GlobalScope;
    }

    public void EnterNewScope() => CurrentScope = CurrentScope.CreateChildScope();
    
    public void ExitScope()
        => CurrentScope = CurrentScope.Parent ?? throw new InvalidOperationException("Cannot exit the global scope.");
    
    public bool TryAddSymbol(Symbol symbol) => CurrentScope.TryAddSymbol(symbol);
    
    public bool TryAddGlobalSymbol(Symbol symbol) => GlobalScope.TryAddSymbol(symbol);
    
    public bool TryLookupSymbol(string name, [MaybeNullWhen(false)] out Symbol symbol)
        => CurrentScope.TryLookupSymbol(name, out symbol);
    
    public bool TryLookupSymbol<T>(string name, [MaybeNullWhen(false)] out T symbol) where T : Symbol?
    {
        if (TryLookupSymbol(name, out Symbol? foundSymbol) && foundSymbol is T correctTypeSymbol)
        {
            symbol = correctTypeSymbol;
            return true;
        }
        
        symbol = null;
        return false;
    }
    
    public bool TryLookupGlobalSymbol(string name, [MaybeNullWhen(false)] out Symbol symbol)
        => GlobalScope.TryLookupSymbol(name, out symbol);

    public bool TryLookupGlobalSymbol<T>(string name, [MaybeNullWhen(false)] out T symbol) where T : Symbol?
    {
        if (TryLookupGlobalSymbol(name, out Symbol? foundSymbol) && foundSymbol is T correctTypeSymbol)
        {
            symbol = correctTypeSymbol;
            return true;
        }
        
        symbol = null;
        return false;
    }
    
    public T GetGlobalSymbol<T>(string name) where T : Symbol
    {
        if (TryLookupGlobalSymbol(name, out Symbol? symbol) && symbol is T correctTypeSymbol)
            return correctTypeSymbol;
        
        throw new InvalidOperationException($"Symbol {name} not found");
    }
    
    public IEnumerable<T> GetGlobalSymbols<T>() where T : Symbol
    {
        foreach (Symbol symbol in GlobalScope.Symbols)
        {
            if (symbol is T correctTypeSymbol)
                yield return correctTypeSymbol;
        }
    }

    public T GetSymbol<T>(string name) where T : Symbol
    {
        if (TryLookupSymbol(name, out Symbol? symbol) && symbol is T correctTypeSymbol)
            return correctTypeSymbol;

        throw new InvalidOperationException($"Symbol {name} not found");
    }
    
    public VerifexType GetType(string name)
    {
        if (TryLookupGlobalSymbol(name, out Symbol? symbol) && symbol.ResolvedType != null)
            return symbol.ResolvedType;
        
        throw new InvalidOperationException($"Type {name} not found");
    }
}

public class Scope(Scope? parent = null)
{
    private readonly Dictionary<string, Symbol> _symbols = new();
    
    public Scope? Parent { get; } = parent;
    
    public IEnumerable<Symbol> Symbols => _symbols.Values;
    
    public bool TryAddSymbol(Symbol symbol) => _symbols.TryAdd(symbol.Name, symbol);

    public bool TryLookupSymbol(string name, [MaybeNullWhen(false)] out Symbol symbol)
    {
        if (_symbols.TryGetValue(name, out symbol))
            return true;
        
        if (Parent != null && Parent.TryLookupSymbol(name, out symbol))
            return true;
        
        symbol = null;
        return false;
    }

    public Scope CreateChildScope() => new(this);
}