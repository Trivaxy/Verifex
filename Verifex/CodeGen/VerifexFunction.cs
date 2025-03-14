using System.Collections.ObjectModel;
using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public class VerifexFunction(string name, ReadOnlyCollection<ParameterInfo> parameters, VerifexType returnType)
    : IEquatable<VerifexFunction>
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<ParameterInfo> Parameters = parameters;
    public readonly VerifexType ReturnType = returnType;

    public override bool Equals(object? obj)
    {
        return Equals(obj as VerifexFunction);
    }

    public bool Equals(VerifexFunction? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        if (!Name.Equals(other.Name)) return false;
        if (!ReturnType.Equals(other.ReturnType)) return false;
        
        if (Parameters.Count != other.Parameters.Count) return false;
        
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].Equals(other.Parameters[i])) return false;
        }
    
        return true;
    }
    
    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Name);
        hash.Add(ReturnType);
        
        foreach (var param in Parameters)
            hash.Add(param);
        
        return hash.ToHashCode();
    }
}