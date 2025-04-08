using System.Reflection;

namespace Verifex.CodeGen.Types;

public class IntegerType : VerifexType
{
    public override string Name => "Int";

    public override TypeInfo IlType => typeof(int).GetTypeInfo();
}

public class RealType : VerifexType
{
    public override string Name => "Real";

    public override TypeInfo IlType => typeof(double).GetTypeInfo();
}

public class StringType : VerifexType
{
    public override string Name => "String";

    public override TypeInfo IlType => typeof(string).GetTypeInfo();
}

public class BoolType : VerifexType
{
    public override string Name => "Bool";

    public override TypeInfo IlType => typeof(bool).GetTypeInfo();
}

public class VoidType : VerifexType
{
    public override string Name => "Void";

    public override TypeInfo IlType => typeof(void).GetTypeInfo();
}