using System.Collections.ObjectModel;
using System.Reflection;

namespace Verifex.CodeGen.Types;

public class ArcheType(string name, ReadOnlyDictionary<string, VerifexFunction> methods, Dictionary<string, FieldInfo> fields) : StructType(name, fields)
{
    public ReadOnlyDictionary<string, VerifexFunction> Methods { get; } = methods;

    public override TypeInfo IlType => typeof(Dictionary<string, object>).GetTypeInfo();
    
    public override VerifexType FundamentalType => this;
}