using System.Collections.ObjectModel;
using System.Reflection;

namespace Verifex.CodeGen.Types;

public class StructType(string name, ReadOnlyDictionary<string, FieldInfo> fields) : VerifexType
{
    public override string Name => name;

    public ReadOnlyDictionary<string, FieldInfo> Fields { get; } = fields;

    public override TypeInfo IlType => typeof(Dictionary<string, object>).GetTypeInfo();

    public override VerifexType FundamentalType => this;
}

public record FieldInfo(string Name, VerifexType Type);