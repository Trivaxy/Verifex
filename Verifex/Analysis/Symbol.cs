using System.Collections.ObjectModel;
using Verifex.CodeGen;
using Verifex.CodeGen.Types;
using Verifex.Parsing;

namespace Verifex.Analysis;

public abstract class Symbol
{
    public required string Name { get; init; }
    
    public required AstNode? DeclaringNode { get; init; }

    public VerifexType? ResolvedType { get; set; } // set by the type annotator pass
}

public class LocalVarSymbol : Symbol
{
    public required bool IsMutable { get; init; }
    
    public required bool IsParameter { get; init; }
    
    public required int Index { get; init; }
}

public class FunctionSymbol : Symbol
{
    public required VerifexFunction Function { get; init; }
}

public class BuiltinFunctionSymbol : FunctionSymbol
{
    public static BuiltinFunctionSymbol Create(BuiltinFunction function) => new BuiltinFunctionSymbol()
    {
        DeclaringNode = null,
        Name = function.Name,
        ResolvedType = null,
        Function = function,
    };
}

public abstract class TypeSymbol : Symbol;

public class BuiltinTypeSymbol : TypeSymbol
{
    public static BuiltinTypeSymbol Create(VerifexType type) => new BuiltinTypeSymbol()
    {
        DeclaringNode = null,
        Name = type.Name,
        ResolvedType = type,
    };
}

public class RefinedTypeSymbol : TypeSymbol
{
    public required VerifexType BaseType { get; init; }
    
    public RefinedTypeValueSymbol ValueSymbol { get; set; } = null!; // set by the binding pass
}

public class RefinedTypeValueSymbol : Symbol;

public class StructSymbol : TypeSymbol
{
    public required ReadOnlyDictionary<string, StructFieldSymbol> Fields { get; init; }
    
    public required ReadOnlyDictionary<string, FunctionSymbol> Methods { get; init; }
}

public class StructFieldSymbol : Symbol
{
    public required StructSymbol Owner { get; set; }
    
    public required int Index { get; init; }
}

public class SelfSymbol : Symbol; // ephemeral symbol that gets used in type checking
