using System;

namespace Unreal.ReplayLib.Exceptions
{
    public class MalformedPacketException : Exception
    {
        public MalformedPacketException()
        {
        }

        public MalformedPacketException(string msg) : base(msg)
        {
        }

        public MalformedPacketException(string msg, Exception exception) : base(msg, exception)
        {
        }
    }
}