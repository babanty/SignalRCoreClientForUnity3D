using System;
using System.Threading.Tasks;

namespace SignalRCoreClientForUnity3D
{
    public interface ISignalRClient
    {
        event Func<string, Task> DisconnectedEvent;

        Task ConnectToServer();
        void DisconnectFromServer();
        bool IsConnected();


        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On<T>(string method, Func<T, Task> action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On<T>(string method, Action<T> action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On<T1, T2>(string method, Func<T1, T2, Task> action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On<T1, T2>(string method, Action<T1, T2> action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On(string method, Func<Task> action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On(string method, Action action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On(string method, Func<object[], Task> action);

        /// <summary> Subscribe to the event of receiving a message from the server </summary>
        void On(string method, Action<object[]> action);



        /// <summary> Sends an empty message and waits for a response </summary>
        /// <param name="method"> SignalR Hub Method on server </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        Task SendMessage(string method);


        /// <summary> Sends a message and waits for a response </summary>
        /// <typeparam name="TRequest"> The container-class containing the request arguments </typeparam>
        /// <typeparam name="TResponse"> The class to which we are waiting in the response </typeparam>
        /// <param name="request"> The container-class containing the request arguments </param>
        /// <param name="method"> SignalR Hub Method on server  </param>
        Task<TResponse> SendMessage<TRequest, TResponse>(string method, TRequest request);


        /// <summary> Sends a message and waits for a response </summary>
        /// <typeparam name="TRequest"> The container-class containing the request arguments </typeparam>
        /// <param name="request"> The container-class containing the request arguments </param>
        /// <param name="method"> SignalR Hub Method on server  </param>
        Task SendMessage<TRequest>(string method, TRequest request);


        /// <summary> Sends an empty message and waits for a response </summary>
        /// <typeparam name="TResponse"> The class to which we are waiting in the response </typeparam>
        /// <param name="method"> SignalR Hub Method on server  </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        Task<TResponse> SendMessage<TResponse>(string method);


        /// <summary> Sends a message and waits for a response to complete the request </summary>
        /// <param name="arguments"> SignalR endpoint arguments on the server </param>
        /// <param name="method"> SignalR Hub Method on server  </param>
        Task SendMessage(string method, params object[] arguments);


        /// <summary>  Sends a message and waits for a response </summary>
        /// <typeparam name="TResponse"> The class to which we are waiting in the response </typeparam>
        /// <param name="arguments"> SignalR endpoint arguments on the server </param>
        /// <param name="method"> SignalR Hub Method on server  </param>
        Task<TResponse> SendMessage<TResponse>(string method, params object[] arguments);
    }
}
