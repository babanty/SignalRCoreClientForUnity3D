using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// NOTE: sources:
// - https://github.com/dotnet/aspnetcore/blob/6c24b7c0d0fd3d4723740564b3b5f67ffbfc77b9/src/SignalR/docs/specs/HubProtocol.md
// - https://github.com/dotnet/aspnetcore/blob/6c24b7c0d0fd3d4723740564b3b5f67ffbfc77b9/src/SignalR/docs/specs/TransportProtocols.md


namespace SignalRCoreClientForUnity3D.Implementation
{
    /// <summary> Все комменты к публичным методам в интерфейсе ISignalRClient </summary>
    internal class SignalRClient : ISignalRClient
    {
        public event Func<SignalRClientResponse, Task> CommonReceivedEvent = (s) => Task.CompletedTask;
        public event Func<string, Task> DisconnectedEvent = (s) => Task.CompletedTask;


        private ClientWebSocket WebSocket { get; set; }
        private CancellationTokenSource ReceiveCancellationTokenSource { get; set; }
        private bool ReceiveOn { get; set; }
        private List<SignalRRequest> SignalRRequests { get; set; } = new List<SignalRRequest>();
        private bool IsHandchakesCompleted { get; set; }
        private readonly object SignalRRequestsLocker = new object();

        /// <summary> [CanBeNull] </summary>
        private readonly ISignalRClientLogger _logger;
        private readonly string _uri;
        private readonly SignalRRequestReceiver _signalRRequestReceiver;


        public SignalRClient(string uri, ISignalRClientLogger logger = null)
        {
            _uri = uri;
            _logger = logger;

            _signalRRequestReceiver = new SignalRRequestReceiver(this, logger);
        }


        public async Task ConnectToServer()
        {
            if (WebSocket != null && (WebSocket.State == WebSocketState.Open || WebSocket.State == WebSocketState.Connecting))
            {
                _logger?.Log(LogLevel.Info, "Socket already opened");
                return;
            }

            try
            {
                await WebSocketConnect();
                await SignalRHandshakes();

                _logger?.Log(LogLevel.Info, $"Server connected.");
            }
            catch (WebSocketException e)
            {
                _logger?.Log(LogLevel.Warning, e, "Server connection failed.");
                throw;
            }
            catch (Exception e)
            {
                _logger?.Log(LogLevel.Warning, e, "Unexpected exception during connected to server.");
                throw;
            }

            Receive();
        }


        public void DisconnectFromServer()
        {
            IsHandchakesCompleted = false;

            lock (SignalRRequestsLocker)
                foreach (var openedRequest in SignalRRequests)
                {
                    if (openedRequest is null) continue;

                    openedRequest.Response = null;
                    openedRequest.Status = SignalRRequestStatus.RequestFailed;                
                }

            ReceiveOn = false;
            ReceiveCancellationTokenSource?.Cancel();

            WebSocket?.Dispose();
        }


        public async Task<TResponse> SendMessage<TRequest, TResponse>(string method, TRequest request)
        {
            var invocationId = GetNewInvocationId();
            await SendStandardTelegram(SignalRTools.GetTelegramToSend(request, method, invocationId), invocationId); 
            return await GetSendMessageResponse<TResponse>(invocationId);
        }


        public async Task SendMessage<TRequest>(string method, TRequest request)
        {
            var invocationId = GetNewInvocationId();
            await SendStandardTelegram(SignalRTools.GetTelegramToSend(request, method, invocationId), invocationId); 
            await WaitSendMessageResult(invocationId);
        }


        public async Task<TResponse> SendMessage<TResponse>(string method, params object[] arguments)
        {
            var invocationId = GetNewInvocationId();
            await SendStandardTelegram(SignalRTools.GetTelegramToSend(method, arguments, invocationId), invocationId); 
            return await GetSendMessageResponse<TResponse>(invocationId);
        }


        public async Task SendMessage(string method, params object[] arguments)
        {
            var invocationId = GetNewInvocationId();
            await SendStandardTelegram(SignalRTools.GetTelegramToSend(method, arguments, invocationId), invocationId);
            await WaitSendMessageResult(invocationId);
        }


        public async Task<TResponse> SendMessage<TResponse>(string method)
        {
            var invocationId = GetNewInvocationId();
            await SendStandardTelegram(SignalRTools.GetTelegramToSend(method, invocationId), invocationId);
            return await GetSendMessageResponse<TResponse>(invocationId);
        }


        public async Task SendMessage(string method)
        {
            var invocationId = GetNewInvocationId();
            await SendStandardTelegram(SignalRTools.GetTelegramToSend(method, invocationId), invocationId);
            await WaitSendMessageResult(invocationId);
        }


        public bool IsConnected() => (WebSocket.State == WebSocketState.Open || WebSocket.State == WebSocketState.Connecting) && IsHandchakesCompleted;


        public void On<T>(string method, Func<T, Task> action) => _signalRRequestReceiver.On(method, action);
        public void On<T>(string method, Action<T> action) => _signalRRequestReceiver.On(method, action);
        public void On<T1, T2>(string method, Func<T1, T2, Task> action) => _signalRRequestReceiver.On(method, action);
        public void On<T1, T2>(string method, Action<T1, T2> action) => _signalRRequestReceiver.On(method, action);
        public void On(string method, Func<Task> action) => _signalRRequestReceiver.On(method, action);
        public void On(string method, Action action) => _signalRRequestReceiver.On(method, action);
        public void On(string method, Func<object[], Task> action) => _signalRRequestReceiver.On(method, action);
        public void On(string method, Action<object[]> action) => _signalRRequestReceiver.On(method, action);


        /// <summary> Получить и везде где надо прокинуть новый код сообщения для отправки в SignalR </summary>
        private string GetNewInvocationId()
        {
            return Guid.NewGuid().ToString();
        }


        /// <summary> Получить ответ на ранее отправленное сообщение </summary>
        /// <typeparam name="TResponse"> тип данных которые ожидаем в ответе </typeparam>
        /// <param name="invocationId"> id телеграммы </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        /// <exception cref="ArgumentNullException"> Response body from server is null, but expected: {typeof(TResponse).Name} </exception>
        private async Task <TResponse> GetSendMessageResponse<TResponse>(string invocationId)
        {
            var requestBody = await GetSendMessageResponse(invocationId);

            if (requestBody is null)
                throw new ArgumentNullException($"Response body from server is null, but expected: {typeof(TResponse).Name}");

            return SignalRTools.ConvertArgument<TResponse>(requestBody);
        }

        /// <summary> Дождаться положительного ответа от сервера, о том что ранее отправленное сообщение получено и обрбаотно </summary>
        /// <param name="invocationId"> id телеграммы </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        private async Task WaitSendMessageResult(string invocationId)
        {
            await GetSendMessageResponse(invocationId); // все ровно какой ответ, главное, что без ошибки
        }


        /// <summary> 
        /// Получить ответ на ранее отправленное сообщение. Это будет только "результат" - тело телеграммы.  
        /// Тело телеграммы может быть типом: Newtonsoft.Json.Linq.JObject или bool или null или int или string
        /// </summary>
        /// <param name="invocationId"> id телеграммы </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        private async Task<object> GetSendMessageResponse(string invocationId)
        {
            SignalRRequest request = null;
            lock (SignalRRequestsLocker)
                request = SignalRRequests.FirstOrDefault(r => r.InvocationId == invocationId);

            if (request is null || request.Status == SignalRRequestStatus.Unknown ||
                                   request.Status == SignalRRequestStatus.Created)
            {
                throw new ArgumentException("Telegram must be sent.");
            }

            while (true)
            {
                if (request.Status == SignalRRequestStatus.RequestFailed)
                {
                    var error = request.Response?.Error;
                    lock (SignalRRequestsLocker)
                        SignalRRequests.Remove(request);
                    throw new SignalRRequestFailedException(error);
                }

                if (request.Status == SignalRRequestStatus.ResponseReceived)
                {
                    var requestBody = request.Response?.Result?.ToString();
                    lock (SignalRRequestsLocker)
                        SignalRRequests.Remove(request);
                    return requestBody;
                }

                await Task.Delay(3);
            }
        }


        private async Task SendStandardTelegram(ArraySegment<byte> telegram, string invocationId)
        {
            if (telegram == default || telegram.Count == 0)
            {
                _logger?.Log(LogLevel.Warning, "Telegram can't be null or empty");
                return;
            }

            var request = new SignalRRequest()
            {
                Status = SignalRRequestStatus.Created,
                InvocationId = invocationId
            };
            lock (SignalRRequestsLocker)
                SignalRRequests.Add(request);

            try
            {
                await SendTelegram(telegram);
            }
            catch (Exception e)
            {
                request.Status = SignalRRequestStatus.RequestFailed;
                throw e;
            }

            request.Status = SignalRRequestStatus.Sent;
        }


        /// <summary> Стандартная отправка сообщения на SignalR-server </summary>
        /// <param name="telegram"> json со всеми необходимыми хедерами (в начале есть NOTE и там ссылки на то какие они должны быть) с битом завершения сообщения </param>
        private async Task SendTelegram(ArraySegment<byte> telegram)
        {
            _logger?.Log(LogLevel.Trace, $"Sending a message: {SignalRTools.DecodeTelegram(telegram)}");
            await WebSocket.SendAsync(telegram, WebSocketMessageType.Text, true, CancellationToken.None);
        }


        private async Task WebSocketConnect()
        {
            WebSocket = new ClientWebSocket();
            var connectionTimeoutInMs = 1500;

            var cancellationTokenSource = new CancellationTokenSource();

            var mainTask = Task.Run(async () => await WebSocket.ConnectAsync(new Uri(_uri), cancellationTokenSource.Token));

            if (await Task.WhenAny(mainTask, Task.Delay(connectionTimeoutInMs, cancellationTokenSource.Token)) == mainTask)
            {
                if (mainTask.IsFaulted)
                    throw mainTask.Exception;

                _logger?.Log(LogLevel.Info, "WebSocket connected");
                return;
            }
            else
            {
                cancellationTokenSource.Cancel();
                _logger?.Log(LogLevel.Info, "WebSocket connection timeout");
                throw new WebSocketException("Timeout");
            }
        }


        private async Task SignalRHandshakes()
        {
            var handshakes = SignalRTools.EncodeTelegram(@"{""protocol"":""json"", ""version"":1}");
            await SendTelegram(handshakes);
            _logger?.Log(LogLevel.Info, "SignalR handshake completed");
            IsHandchakesCompleted = true;
        }


        private void Receive()
        {
            var ReceiveCancellationTokenSource = new CancellationTokenSource();
            var ReceiveCancellationToken = ReceiveCancellationTokenSource.Token;
            ReceiveOn = true;

            var task = Task.Run(async () =>
            {
                var fullResponse = ""; // полный ответ, на случай если он был разбит на не сколько строк
                while (ReceiveOn)
                {
                    ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[1024]);

                    try
                    {
                        var result = await WebSocket.ReceiveAsync(bytesReceived, ReceiveCancellationToken);

                        var response = Encoding.UTF8.GetString(bytesReceived.Array, 0, result.Count);

                        _logger?.Log(LogLevel.Trace, $"response from server: {response}");

                        // проверка на дисконнект
                        if (string.IsNullOrEmpty(response) && WebSocket.State == WebSocketState.CloseReceived)
                        {
                            var msg = "Disconnected by server request. If you did not call the connection reset, then check that Server-method returns something.";
                            _logger?.Log(LogLevel.Info, msg);
                            DisconnectedEvent?.Invoke(msg);
                            break;
                        }

                        fullResponse += response;

                        // если не пришел символ окончания телеграммы то заканчиваем итерацию, чтобы в последующих собрать всю телеграмму и только тогда ее обработать
                        if (response[response.Length - 1] != SignalRTools.TelegramSeparatorChar)
                            continue;

                        var deserializedResponse = ConvertSignalRResponse(fullResponse);

                        fullResponse = ""; // сбрасываем коллектор всех кусков ответов

                        // если это ответ сервером на запрос от клиента
                        bool isItResposne = false;
                        lock (SignalRRequestsLocker)
                            isItResposne = SignalRRequests.Any(r => r.InvocationId == deserializedResponse.InvocationId);
                        if (isItResposne)
                                SetResponseToRequest(deserializedResponse);
                        // если это запрос от сервера клиенту
                        else
                        {
                            _ = CommonReceivedEvent.Invoke(deserializedResponse);

                            // если сервер ожидает ответ
                            if (deserializedResponse.Type == SignalRCallTypes.Invocation && !string.IsNullOrEmpty(deserializedResponse.InvocationId))
                                _logger?.Log(LogLevel.Trace, "I am not able to send a response to the server about completion"); // TODO [NotImpl] отправлять ответ серверу о завершении
                        }
                            

                    }
                    catch (WebSocketException e)
                    {
                        _logger?.Log(LogLevel.Info, e, "Disconnected by exception");
                        await DisconnectedEvent.Invoke(e.Message);
                        break;
                    }
                    catch (Exception e)
                    {
                        _logger?.Log(LogLevel.Warning, e, "Unexpected exception during receive message from server.");
                        return;
                    }

                    await Task.Delay(2);
                }
            });
        }


        /// <summary> Очистить телеграмму от служебного "мусора" </summary>
        private string GetCleanedUpResponse(string fullResponse)
        {
            return fullResponse.Trim(SignalRTools.TelegramSeparatorChar); // убираем из ответа символ окончания телеграммы
        }


        /// <summary> Конвертировать неочищенный ответ от сервера в соответствующий объект </summary>
        private SignalRClientResponse ConvertSignalRResponse(string fullResponse)
        {
            var cleanedUpResponse = GetCleanedUpResponse(fullResponse);
            return JsonConvert.DeserializeObject<SignalRClientResponse>(cleanedUpResponse);
        }


        /// <summary> Вставить ответ на запрос в запрос, который будет найден по id внутри ответа </summary>
        private void SetResponseToRequest(SignalRClientResponse response)
        {
            var request = SignalRRequests.FirstOrDefault(r => r.InvocationId == response.InvocationId);
            if(request is null)
                throw new Exception("Attempting to insert a response into a non-existent request");

            request.Response = response;

            if (string.IsNullOrEmpty(response.Error))
                request.Status = SignalRRequestStatus.ResponseReceived;
            else
                request.Status = SignalRRequestStatus.RequestFailed;
        }
    }
}
