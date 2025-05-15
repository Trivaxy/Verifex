using System.Collections.ObjectModel;
using System.Reflection;

namespace Verifex.CodeGen.Types;

public class StructType(string name, Dictionary<string, FieldInfo> fields) : VerifexType
{
    public override string Name => name;

    public Dictionary<string, FieldInfo> Fields { get; } = fields; // not readonly bc embedded structs

    public override TypeInfo IlType => typeof(Dictionary<string, object>).GetTypeInfo();

    public override VerifexType FundamentalType => this;
}

public record FieldInfo(string Name, VerifexType Type);