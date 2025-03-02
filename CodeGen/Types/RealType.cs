using System.Reflection;

namespace Verifex.CodeGen.Types;

public class RealType : VerifexType
{
    public override string Name => "Real";

    public override TypeInfo IlType => typeof(double).GetTypeInfo();
}