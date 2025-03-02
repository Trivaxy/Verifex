using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public readonly struct ValueLocation(VerifexType type, int index)
{
    public readonly VerifexType Type = type;
    public readonly int Index = index;
}