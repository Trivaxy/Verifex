using System.Reflection;

namespace Verifex.CodeGen.Types;

public class MaybeType : VerifexType
{
    public readonly SortedSet<VerifexType> Types;

    public override string Name { get; }

    public override TypeInfo IlType => typeof(object).GetTypeInfo();

    public override VerifexType FundamentalType => this;
    
    public MaybeType(IEnumerable<VerifexType> types)
    {
        Types = new SortedSet<VerifexType>(types, Comparer<VerifexType>.Create((t1, t2) => string.Compare(t1.Name, t2.Name, StringComparison.Ordinal)));
        Name = Types.Select(type => type.Name).Aggregate((a, b) => $"{a} or {b}");
    }

    public override bool Equals(object? obj)
    {
        return obj is MaybeType other && Equals(other);
    }

    public bool Equals(MaybeType? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        if (Types.Count != other.Types.Count) return false;

        foreach (var type in Types)
            if (!other.Types.Contains(type)) return false;

        return true;
    }
    
    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        foreach (var type in Types)
            hash.Add(type);
        return hash.ToHashCode();
    }
}