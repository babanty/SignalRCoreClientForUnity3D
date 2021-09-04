using System;

namespace SignalRCoreClientForUnity3D
{
    public interface ISignalRClientLogger
    {
        void Log(LogLevel logLevel, string message);
        void Log(LogLevel logLevel, Exception e, string message);
    }


    public enum LogLevel
    {
        Trace = 0,
        Info = 1,
        Warning = 2
    }
}
