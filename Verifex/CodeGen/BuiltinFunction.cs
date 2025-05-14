using System.Reflection;
using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public class BuiltinFunction(string name, IList<ParameterInfo> parameters, VerifexType returnType, MethodInfo method) : VerifexFunction(name, parameters, returnType, null, false)
{
    public readonly MethodInfo Method = method;
}