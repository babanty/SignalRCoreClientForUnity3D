namespace SignalRCoreClientForUnity3D.Implementation
{
    public class SignalRClientResponse
    {
        public SignalRCallTypes? Type { get; set; }
        public string InvocationId { get; set; }
        public string Error { get; set; }


        /// <summary> Can be: Newtonsoft.Json.Linq.JObject or bool or null or int or string </summary>
        public object Result { get; set; }


        /// <summary> это если сервер указывает конкретный метод на клиенте </summary>
        public string Target { get; set; }


        public object[] Arguments { get; set; }
    }
}
