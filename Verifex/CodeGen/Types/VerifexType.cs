using System;
using System.Reflection;

namespace Verifex.CodeGen.Types;

public abstract class VerifexType : IEquatable<VerifexType>
{
    public abstract string Name { get; }
    
    public abstract TypeInfo IlType { get; }
    
    public virtual VerifexType EffectiveType => this;

    public abstract VerifexType FundamentalType { get; }
    
    public static readonly VerifexType Unknown = new UnknownType();
    
    public static readonly VerifexType Empty = new EmptyType();

    public static VerifexType Delayed(Func<VerifexType> resolver) => new DelayedType(resolver);

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

    public static bool operator !=(VerifexType? left, VerifexType? right) => !(left == right);
    
    private class DelayedType(Func<VerifexType> resolver) : VerifexType
    {
        private VerifexType _resolvedType = Unknown;

        public override string Name
        {
            get
            {
                EnsureResolved();
                return _resolvedType.Name;
            }
        }

        public override TypeInfo IlType
        {
            get
            {
                EnsureResolved();
                return _resolvedType.IlType;
            }
        }

        public override VerifexType EffectiveType
        {
            get
            {
                EnsureResolved();
                return _resolvedType.EffectiveType;
            }
        }
        
        public override VerifexType FundamentalType
        {
            get
            {
                EnsureResolved();
                return _resolvedType.FundamentalType;
            }
        }

        private void EnsureResolved()
        {
            if (_resolvedType != Unknown) return;
            _resolvedType = resolver();
        }

        public override bool Equals(object? obj) => EffectiveType.Equals(obj);
        
        public override int GetHashCode() => EffectiveType.GetHashCode();
    }
}