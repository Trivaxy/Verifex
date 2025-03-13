using System;
using System.Reflection;

namespace Verifex.CodeGen.Types;

public abstract class VerifexType : IEquatable<VerifexType>
{
    public abstract string Name { get; }
    
    public abstract TypeInfo IlType { get; }

    public override bool Equals(object? obj)
    {
        return obj is VerifexType other && Equals(other);
    }

    public bool Equals(VerifexType? other)
    {
        if (other is null)
            return false;
            
        return Name == other.Name && IlType.Equals(other.IlType);
    }

    public override int GetHashCode() => HashCode.Combine(Name, IlType);

    public static bool operator ==(VerifexType? left, VerifexType? right)
    {
        if (left is null)
            return right is null;

        return left.Equals(right);
    }

    public static bool operator !=(VerifexType? left, VerifexType? right)
    {
        return !(left == right);
    }
}