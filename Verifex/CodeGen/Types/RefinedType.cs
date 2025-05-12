using System.Reflection;
using Verifex.Parsing;

namespace Verifex.CodeGen.Types;

public class RefinedType(string name, VerifexType baseType, AstNode rawConstraint) : VerifexType
{
    public override string Name { get; } = name;

    public VerifexType BaseType { get; } = baseType;
    
    public AstNode RawConstraint { get; } = rawConstraint;
    
    public override TypeInfo IlType => BaseType.IlType;
    
    public override VerifexType FundamentalType => BaseType.FundamentalType;
}