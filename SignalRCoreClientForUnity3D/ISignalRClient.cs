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


        /// <summary> Подписаться на событие получения сообщения от сервера на указанный метод </summary>
        void On<T>(string method, Func<T, Task> action);

        /// <summary> Подписаться на событие получения сообщения от сервера на указанный метод </summary>
        void On<T>(string method, Action<T> action);

        /// <summary> Подписаться на событие получения сообщения от сервера на указанный метод </summary>
        void On<T>(string method, Func<Task> action);

        /// <summary> Подписаться на событие получения сообщения от сервера на указанный метод </summary>
        void On<T>(string method, Action action);



        /// <summary> Отправляет пустое сообщение и дожидается ответа </summary>
        /// <param name="method"> Метод Hub-а SignalR-а на сервере </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        Task SendMessage(string method);


        /// <summary> Отправляет сообщение и дожидается ответа, после чего его отдает </summary>
        /// <typeparam name="TRequest"> Класс-контейнер, содержащий в себе аргументы запроса </typeparam>
        /// <typeparam name="TResponse"> Класс, в который ждем в ответе </typeparam>
        /// <param name="request"> Класс-контейнер, содержащий в себе аргументы запроса </param>
        /// <param name="method"> Метод Hub-а SignalR-а на сервере </param>
        Task<TResponse> SendMessage<TRequest, TResponse>(string method, TRequest request);


        /// <summary> Отправляет сообщение и дожидается ответа </summary>
        /// <typeparam name="TRequest"> Класс-контейнер, содержащий в себе аргументы запроса </typeparam>
        /// <param name="request"> Класс-контейнер, содержащий в себе аргументы запроса </param>
        /// <param name="method"> Метод Hub-а SignalR-а на сервере </param>
        Task SendMessage<TRequest>(string method, TRequest request);


        /// <summary> Отправляет пустое сообщение и дожидается ответа </summary>
        /// <typeparam name="TResponse"> тип класса ответ-а от сервера </typeparam>
        /// <param name="method"> Метод Hub-а SignalR-а на сервере </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        Task<TResponse> SendMessage<TResponse>(string method);


        /// <summary> Отправляет сообщение и дожидается ответа о выполнении запроса </summary>
        /// <param name="arguments"> аргументы endpoint-a (метода) SignalR-а на сервере </param>
        /// <param name="method"> Метод Hub-а SignalR-а на сервере </param>
        Task SendMessage(string method, params object[] arguments);


        /// <summary> Отправляет сообщение и дожидается ответа </summary>
        /// <typeparam name="TResponse"> тип класса ответ-а от сервера </typeparam>
        /// <param name="arguments"> аргументы endpoint-a (метода) SignalR-а на сервере </param>
        /// <param name="method"> Метод Hub-а SignalR-а на сервере </param>
        Task<TResponse> SendMessage<TResponse>(string method, params object[] arguments);
    }
}
