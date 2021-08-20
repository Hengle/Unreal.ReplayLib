using System;
using Unreal.ReplayLib.IO;

namespace Unreal.ReplayLib.Models.Properties
{
    public class FDateTime : IProperty
    {
        public DateTimeOffset Time { get; private set; }

        public void Serialize(NetBitReader reader) => Time = reader.ReadDate();
    }
}