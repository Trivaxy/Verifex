using Verifex.CodeGen;
using Verifex.CodeGen.Types;

namespace Verifex.Analysis.Symbols;

public class Symbols
{
    private readonly Dictionary<string, VerifexFunction> _functions = new Dictionary<string, VerifexFunction>();
    private readonly Dictionary<string, ValueLocation> _locals = new Dictionary<string, ValueLocation>();
    private readonly Dictionary<string, VerifexType> _types = new Dictionary<string, VerifexType>();

    public Symbols() => RegisterCoreTypes();
    
    public void AddFunction(VerifexFunction function) => _functions.Add(function.Name, function);

    public ValueLocation AddLocal(string name, VerifexType type)
    {
        ValueLocation value = new ValueLocation(type, _locals.Count);
        _locals.Add(name, value);

        return value;
    }
    
    public void AddType(VerifexType type) => _types.Add(type.Name, type);
    
    public VerifexFunction GetFunction(string name) => _functions[name];
    
    public ValueLocation GetLocal(string name) => _locals[name];
    
    public VerifexType GetType(string name) => _types[name];
    
    public void ClearLocals() => _locals.Clear();

    private void RegisterCoreTypes()
    {
        AddType(new IntegerType());
        AddType(new RealType());
        AddType(new VoidType());
    }
}