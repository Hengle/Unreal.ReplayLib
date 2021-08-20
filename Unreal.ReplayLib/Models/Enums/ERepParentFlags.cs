using System;

namespace Unreal.ReplayLib.Models.Enums
{
    [Flags]
    public enum ERepParentFlags : uint
    {
        None						= 0,
        IsLifetime					= (1 << 0),	 //! This property is valid for the lifetime of the object (almost always set).
        IsConditional				= (1 << 1),	 //! This property has a secondary condition to check
        IsConfig					= (1 << 2),	 //! This property is defaulted from a config file
        IsCustomDelta				= (1 << 3),	 //! This property uses custom delta compression. Mutually exclusive with IsNetSerialize.
        IsNetSerialize				= (1 << 4),  //! This property uses a custom net serializer. Mutually exclusive with IsCustomDelta.
        IsStructProperty			= (1 << 5),	 //! This property is a FStructProperty.
        IsZeroConstructible			= (1 << 6),	 //! This property is ZeroConstructible.
        IsFastArray					= (1 << 7),	 //! This property is a FastArraySerializer. This can't be a ERepLayoutCmdType, because
        //! these Custom Delta structs will have their inner properties tracked.
        HasObjectProperties			= (1 << 8),  //! This property is tracking UObjects (may be through nested properties).
        HasNetSerializeProperties	= (1 << 9),  //! This property contains Net Serialize properties (may be through nested properties).
        HasDynamicArrayProperties   = (1 << 10), //! This property contains Dynamic Array properties (may be through nested properties).
    };
}