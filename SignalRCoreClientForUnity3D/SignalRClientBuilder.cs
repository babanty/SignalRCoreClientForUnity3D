using SignalRCoreClientForUnity3D.Implementation;
using System;

namespace SignalRCoreClientForUnity3D
{
    public class SignalRClientBuilder
    {
        private string Url { get; set; }
        private ISignalRClientLogger Logger { get; set; }

        public virtual ISignalRClient Build()
        {
            if (string.IsNullOrEmpty(Url))
                throw new ArgumentException("The Url cannot be null or empty. Use th \"WithUrl\" method.");

            return new SignalRClient(Url, Logger);
        }


        public SignalRClientBuilder WithUrl(string url)
        {
            Url = url;
            return this;
        }


        public SignalRClientBuilder AddLogger(ISignalRClientLogger logger)
        {
            Logger = logger;
            return this;
        }
    }
}
