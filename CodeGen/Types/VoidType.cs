using System.Reflection;

namespace Verifex.CodeGen.Types;

public class VoidType : VerifexType
{
    public override string Name => "Void";

    public override TypeInfo IlType => typeof(void).GetTypeInfo();
}