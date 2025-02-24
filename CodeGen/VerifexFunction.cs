using System.Collections.ObjectModel;
using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public class VerifexFunction(string name, ReadOnlyCollection<ParameterInfo> parameters, VerifexType returnType)
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<ParameterInfo> Parameters = parameters;
    public readonly VerifexType ReturnType = returnType;
}