using System.Reflection.Emit;
using Verifex.CodeGen.Types;

namespace Verifex.CodeGen;

public readonly record struct ValueLocation(VerifexType ValueType, int Index, LocationType LocationType)
{
    public void EmitLoad(ILGenerator il)
    {
        if (LocationType == LocationType.Local)
            il.Emit(OpCodes.Ldloc, Index);
        else if (LocationType == LocationType.Parameter)
            il.Emit(OpCodes.Ldarg, Index);
    }
}

public enum LocationType
{
    Local,
    Parameter,
}