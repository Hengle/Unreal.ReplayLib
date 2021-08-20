using System;

namespace Unreal.ReplayLib.Exceptions
{
    public class InvalidReplayException : Exception
    {
        public InvalidReplayException()
        {
        }

        public InvalidReplayException(string msg) : base(msg)
        {
        }
    }
}