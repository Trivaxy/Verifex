using System.Collections.ObjectModel;
using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public class VerifexFunction(string name, IList<ParameterInfo> parameters, VerifexType? returnType, VerifexType? owner, bool isStatic)
    : IEquatable<VerifexFunction>
{
    public readonly string Name = name;
    public readonly ReadOnlyCollection<ParameterInfo> Parameters = new(parameters);
    public readonly VerifexType? ReturnType = returnType;
    public readonly VerifexType? Owner = owner;
    public readonly bool IsStatic = isStatic;

    public override bool Equals(object? obj)
    {
        return Equals(obj as VerifexFunction);
    }

    public bool Equals(VerifexFunction? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        if (!Name.Equals(other.Name)) return false;
        if (ReturnType != null && other.ReturnType != null && !ReturnType.Equals(other.ReturnType)) return false;
        if (ReturnType == null && other.ReturnType != null) return false;
        if (ReturnType != null && other.ReturnType == null) return false;
        if (Owner != null && other.Owner != null && !Owner.Equals(other.Owner)) return false;
        if (Owner == null && other.Owner != null) return false;
        if (Owner != null && other.Owner == null) return false;
        if (IsStatic != other.IsStatic) return false;
        
        if (Parameters.Count != other.Parameters.Count) return false;
        
        for (int i = 0; i < Parameters.Count; i++)
            if (!Parameters[i].Equals(other.Parameters[i])) return false;
    
        return true;
    }
    
    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(Name);
        hash.Add(ReturnType);
        hash.Add(Owner);
        hash.Add(IsStatic);
        
        foreach (var param in Parameters)
            hash.Add(param);
        
        return hash.ToHashCode();
    }
}