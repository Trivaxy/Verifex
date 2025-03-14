using System.Collections.ObjectModel;
using System.Reflection;
using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public class BuiltinFunction(string name, ReadOnlyCollection<ParameterInfo> parameters, VerifexType returnType, MethodInfo method) : VerifexFunction(name, parameters, returnType)
{
    public readonly MethodInfo Method = method;
}