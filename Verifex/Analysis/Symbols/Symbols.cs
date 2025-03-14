using Verifex.CodeGen;
using Verifex.CodeGen.Types;

namespace Verifex.Analysis.Symbols;


public class Symbols
{
    private readonly Dictionary<string, VerifexFunction> _functions = new Dictionary<string, VerifexFunction>();
    private readonly Dictionary<string, VerifexType> _types = new Dictionary<string, VerifexType>();
    private readonly Stack<Scope> _scopes = new Stack<Scope>();

    public Symbols() => RegisterCoreSymbols();
    
    public IEnumerable<VerifexFunction> Functions => _functions.Values;
    
    public IEnumerable<VerifexType> Types => _types.Values;
    
    public void AddFunction(VerifexFunction function) => _functions.Add(function.Name, function);
    
    public void AddType(VerifexType type) => _types.Add(type.Name, type);
    
    public ValueLocation AddLocalToCurrentScope(string name, VerifexType type) => _scopes.Peek().AddLocal(name, type);
    
    public VerifexFunction? GetFunction(string name) => _functions.GetValueOrDefault(name);

    public ValueLocation? GetLocal(string name)
    {
        foreach (Scope scope in _scopes)
        {
            if (scope.HasLocal(name))
                return scope.GetLocal(name);
        }
        
        return null;
    }
    public VerifexType? GetType(string name) => _types.GetValueOrDefault(name);
    

    public void PushScope() => _scopes.Push(new Scope());

    public void PopScope() => _scopes.Pop();

    private void RegisterCoreSymbols()
    {
        RegisterCoreTypes();
        RegisterCoreFunctions();
    }
    
    private void RegisterCoreTypes()
    {
        AddType(new VoidType());
        AddType(new IntegerType());
        AddType(new RealType());
        AddType(new StringType());
    }

    private void RegisterCoreFunctions()
    {
        // print function
        VerifexFunction printFunction = new VerifexFunction(
            "print",
            new List<ParameterInfo> { new ParameterInfo("value", GetType("String")) }.AsReadOnly(),
            GetType("Void"),
            true);
        
        AddFunction(printFunction);
    }

    private class Scope
    {
        private readonly Dictionary<string, ValueLocation> _locals = new Dictionary<string, ValueLocation>();
        
        public ValueLocation AddLocal(string name, VerifexType type)
        {
            ValueLocation value = new ValueLocation(type, _locals.Count);
            _locals.Add(name, value);

            return value;
        }
        
        public ValueLocation GetLocal(string name) => _locals[name];

        public bool HasLocal(string name) => _locals.ContainsKey(name);
    }
}