using System.Reflection;

namespace Verifex.CodeGen.Types;

public abstract class VerifexType
{
    public abstract string Name { get; }
    
    public abstract TypeInfo IlType { get; }
}