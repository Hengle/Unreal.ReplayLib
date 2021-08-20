using System;

namespace Unreal.ReplayLib.Models.Enums
{
    [Flags]
    public enum ERepLayoutFlags
    {
        None								= 0,
        IsActor 							= (1 << 0),	//! This RepLayout is for AActor or a subclass of AActor.
        PartialPushSupport					= (1 << 1),	//! This RepLayout has some properties that use Push Model and some that don't.
        FullPushSupport						= (1 << 2),	//! All properties in this RepLayout use Push Model.
        HasObjectOrNetSerializeProperties	= (1 << 3),	//! Will be set for any RepLayout that contains Object or Net Serialize property commands.
        NoReplicatedProperties				= (1 << 4), //! Will be set if the RepLayout has no lifetime properties, or they are all disabled.
    }
}