using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignalRCoreClientForUnity3D.Implementation
{
    internal class SignalRRequestReceiver
    {
        private readonly SignalRClient _signalRClient;
        /// <summary> [CanBeNull] </summary>
        private readonly ISignalRClientLogger _logger;


        /// <summary> обработчики запросов от сервера, где в виде словаря, где key это метод на который отправили запрос (target), а value - обработчик </summary>
        private Dictionary<string, Func<object[], Task>> AsyncRequestHandlers { get; set; } = new Dictionary<string, Func<object[], Task>>();

        /// <summary> обработчики запросов от сервера, где в виде словаря, где key это метод на который отправили запрос (target), а value - обработчик </summary>
        private Dictionary<string, Action<object[]>> RequestHandlers { get; set; } = new Dictionary<string, Action<object[]>>();


        public SignalRRequestReceiver(SignalRClient signalRClient, ISignalRClientLogger logger = null)
        {
            _signalRClient = signalRClient;
            _logger = logger;
            _signalRClient.CommonReceivedEvent += async (args) => { await SignalRServerRequestEventHandler(args); };
        }


        public void On<T>(string method, Func<T, Task> action)
        {
            if (action is null) return;

            AsyncRequestHandlers[method] = async (o) => await action.Invoke(CastArgument<T>(TakeFirstArgument(o)));
        }

        public void On<T>(string method, Action<T> action)
        {
            if (action is null) return;

            RequestHandlers[method] = (o) => action.Invoke(CastArgument<T>(TakeFirstArgument(o)));
        }


        public void On<T1, T2>(string method, Func<T1, T2, Task> action)
        {
            if (action is null) return;

            AsyncRequestHandlers[method] = async (o) => await action.Invoke(CastArgument<T1>(TakeFirstArgument(o)),
                                                                            CastArgument<T2>(TakeSecondArgument(o)));
        }

        public void On<T1, T2>(string method, Action<T1, T2> action)
        {
            if (action is null) return;

            RequestHandlers[method] = (o) => action.Invoke(CastArgument<T1>(TakeFirstArgument(o)),
                                                           CastArgument<T2>(TakeSecondArgument(o)));
        }


        public void On(string method, Func<Task> action)
        {
            if (action is null) return;

            AsyncRequestHandlers[method] = async (o) => await action.Invoke();
        }

        public void On(string method, Action action)
        {
            if (action is null) return;

            RequestHandlers[method] = (o) => action.Invoke();
        }

        public void On(string method, Func<object[], Task> action)
        {
            if (action is null) return;

            AsyncRequestHandlers[method] = action;
        }

        public void On(string method, Action<object[]> action)
        {
            if (action is null) return;

            RequestHandlers[method] = action;
        }


        private async Task SignalRServerRequestEventHandler(SignalRClientResponse request)
        {
            if (!string.IsNullOrEmpty(request.Error))
                _logger?.Log(LogLevel.Warning, request.Error);

            if (request.Type != SignalRCallTypes.Invocation)
            {
                _logger?.Log(LogLevel.Info, $"From server: request.Type: {request.Type}; request.Result: {request.Result}");
                return;
            }

            if (string.IsNullOrEmpty(request.Target) || !RequestHandlers.ContainsKey(request.Target))
            {
                _logger?.Log(LogLevel.Warning, $"Sent a request for a non-existent endpoint on the client: {request.Target}");
                return;
            }


            await SignalRServerRequestResolver(request.Target, request.Arguments);
        }


        private T CastArgument<T>(object argument)
        {
            return SignalRTools.ConvertArgument<T>(argument);
        }


        private object TakeFirstArgument(object[] arguments) => TakeArgument(arguments, 0);
        private object TakeSecondArgument(object[] arguments) => TakeArgument(arguments, 1);


        private object TakeArgument(object[] arguments, int position)
        {
            if (position < 0 || position > arguments.Length - 1)
                return null;

            return arguments[position];
        }


        private async Task SignalRServerRequestResolver(string method, object[] args)
        {
            if (RequestHandlers.ContainsKey(method))
                RequestHandlers[method].Invoke(args);

            if (AsyncRequestHandlers.ContainsKey(method))
                await AsyncRequestHandlers[method].Invoke(args);
        }
    }
}
