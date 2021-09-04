using System;

namespace SignalRCoreClientForUnity3D
{
    public class SignalRRequestFailedException : Exception
    {
        public SignalRRequestFailedException() { }

        public SignalRRequestFailedException(string message) : base(message) { }
    }
}
