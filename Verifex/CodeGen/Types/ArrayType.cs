using System.Reflection;

namespace Verifex.CodeGen.Types;

public class ArrayType(VerifexType elementType) : VerifexType
{
    public override string Name => elementType.FundamentalType is MaybeType ? $"({elementType.Name})[]" : $"{elementType.Name}[]";

    public override TypeInfo IlType => typeof(List<object>).GetTypeInfo();

    public override VerifexType FundamentalType => this;

    public VerifexType ElementType => elementType;

    public override bool Equals(object? obj)
    {
        return obj is ArrayType array && Equals(array);
    }
    
    public bool Equals(ArrayType other)
    {
        return base.Equals(other) && ElementType.Equals(other.ElementType);
    }

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ElementType);
}