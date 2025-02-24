using System.Reflection;

namespace Verifex.CodeGen.Types;

public class IntegerType : VerifexType
{
    public override string Name => "Int";

    public override TypeInfo IlType => typeof(int).GetTypeInfo();
}