using System.Reflection;

namespace Verifex.CodeGen.Types;

public class StringType : VerifexType
{
    public override string Name => "String";

    public override TypeInfo IlType => typeof(string).GetTypeInfo();
}