using System;

namespace Unreal.ReplayLib.Models.Enums
{
    [Flags]
    public enum FTextType
    {
        Transient = 1 << 0,
        CultureInvariant = 1 << 1,
        ConvertedProperty = 1 << 2,
        Immutable = 1 << 3,
        InitializedFromString = 1 << 4 // this ftext was initialized using FromString
    }
}