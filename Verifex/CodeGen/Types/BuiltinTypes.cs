using System.Reflection;

namespace Verifex.CodeGen.Types;

public class IntegerType : VerifexType
{
    public override string Name => "Int";

    public override TypeInfo IlType => typeof(int).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}

public class RealType : VerifexType
{
    public override string Name => "Real";

    public override TypeInfo IlType => typeof(double).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}

public class StringType : VerifexType
{
    public override string Name => "String";

    public override TypeInfo IlType => typeof(string).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}

public class BoolType : VerifexType
{
    public override string Name => "Bool";

    public override TypeInfo IlType => typeof(bool).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}

public class AnyType : VerifexType
{
    public override string Name => "Any";
    
    public override TypeInfo IlType => typeof(object).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}

public class UnknownType : VerifexType
{
    public override string Name => "unknown";

    public override TypeInfo IlType => typeof(void).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}

// This is a type that gets applied to empty array literals to avoid getting ArrayType<unknown>, which causes issues
public class EmptyType : VerifexType
{
    public override string Name => "empty";

    public override TypeInfo IlType => typeof(List<>).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}