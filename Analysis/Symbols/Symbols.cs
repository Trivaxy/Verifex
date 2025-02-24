using Verifex.CodeGen;
using Verifex.CodeGen.Types;

namespace Verifex.Analysis.Symbols;

public class Symbols
{
    private readonly Dictionary<string, VerifexFunction> _functions = new Dictionary<string, VerifexFunction>();
    private readonly Dictionary<string, VerifexType> _locals = new Dictionary<string, VerifexType>();
    private readonly Dictionary<string, VerifexType> _types = new Dictionary<string, VerifexType>();

    public Symbols() => RegisterCoreTypes();
    
    public void AddFunction(VerifexFunction function) => _functions.Add(function.Name, function);
    
    public void AddLocal(string name, VerifexType type) => _locals.Add(name, type);
    
    public void AddType(VerifexType type) => _types.Add(type.Name, type);
    
    public VerifexFunction GetFunction(string name) => _functions[name];
    
    public VerifexType GetLocal(string name) => _locals[name];
    
    public VerifexType GetType(string name) => _types[name];

    private void RegisterCoreTypes()
    {
        AddType(new IntegerType());
        AddType(new VoidType());
    }
}