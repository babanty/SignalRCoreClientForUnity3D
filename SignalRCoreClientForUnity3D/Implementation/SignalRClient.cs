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
    /// <summary> All comments to public methods in the ISignalRClient interface </summary>
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


        /// <summary> SignalR message id </summary>
        private string GetNewInvocationId()
        {
            return Guid.NewGuid().ToString();
        }


        /// <summary> Get a reply to a previously sent message </summary>
        /// <typeparam name="TResponse"> the type of data expected in the response </typeparam>
        /// <param name="invocationId"> telegram id </param>
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

        /// <summary> Wait for a positive response from the server, stating that the previously sent message has been received and processed. </summary>
        /// <param name="invocationId"> telegram id </param>
        /// <exception cref="ArgumentException"> Telegram must be sent. </exception>
        /// <exception cref="SignalRRequestFailedException"/>
        private async Task WaitSendMessageResult(string invocationId)
        {
            await GetSendMessageResponse(invocationId); // no matter what the answer is, the main thing is that without error
        }


        /// <summary> 
        /// Receive a reply to a previously sent message. This will only be the "result" - the body of the telegram.  
        /// The telegram body can be of type: Newtonsoft.Json.Linq.JObject or bool or null or int or string
        /// </summary>
        /// <param name="invocationId"> telegram id </param>
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


        /// <summary> Standard message sending to SignalR-server </summary>
        /// <param name="telegram"> json with all the necessary headers with the message completion bit (there is a NOTE in the beginning and there are links to what they should be)  </param>
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
                var fullResponseCollector = new StringBuilder(); // complete answer, in case it was split into several lines
                while (ReceiveOn)
                {
                    try
                    {
                        await ReceiveIteration(ReceiveCancellationToken, fullResponseCollector);
                    }
                    catch (WebSocketException e)
                    {
                        _logger?.Log(LogLevel.Warning, e, "Disconnected by exception");
                        await DisconnectedEvent.Invoke(e.Message);
                        break;
                    }
                    catch (DisconnectException)
                    {
                        var msg = "Disconnected by server request. If you did not call the connection reset, then check that Server-method returns something.";
                        _logger?.Log(LogLevel.Info, msg);
                        DisconnectedEvent?.Invoke(msg);
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


        private async Task ReceiveIteration(CancellationToken ReceiveCancellationToken, StringBuilder fullResponseCollector)
        {
            ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[1024]);

            var result = await WebSocket.ReceiveAsync(bytesReceived, ReceiveCancellationToken);

            var response = Encoding.UTF8.GetString(bytesReceived.Array, 0, result.Count);

            _logger?.Log(LogLevel.Trace, $"response from server: {response}");


            if (string.IsNullOrEmpty(response) && WebSocket.State == WebSocketState.CloseReceived)
                throw new DisconnectException();

            fullResponseCollector.Append(response);

            // if the symbol of the end of the telegram has not arrived, then we end the iteration. In subsequent iterations, the entire telegram will be collected.
            if (response[response.Length - 1] != SignalRTools.TelegramSeparatorChar)
                return;

            var deserializedResponse = ConvertSignalRResponse(fullResponseCollector.ToString());

            fullResponseCollector.Clear();

            // if this is a server response to a request from a client
            bool isItResposne = false;
            lock (SignalRRequestsLocker)
                isItResposne = SignalRRequests.Any(r => r.InvocationId == deserializedResponse.InvocationId);
            if (isItResposne)
                SetResponseToSuitableRequest(deserializedResponse);

            // if this is a request from the server to the client
            else
            {
                _ = CommonReceivedEvent.Invoke(deserializedResponse);

                // if the server expects a response
                if (deserializedResponse.Type == SignalRCallTypes.Invocation && !string.IsNullOrEmpty(deserializedResponse.InvocationId))
                    _logger?.Log(LogLevel.Warning, "Not implemented. Not able to send a response to the server about completion"); // TODO [NotImpl] это сценарий, если сервер что-то запросил у клиента и сразу же ждет ответа
            }
        }


        /// <summary> Clear the telegram from service debris. </summary>
        private string GetCleanedUpResponse(string fullResponse)
        {
            return fullResponse.Trim(SignalRTools.TelegramSeparatorChar); // remove the end of the telegram from the response
        }


        /// <summary> Convert an unprepared responce from the server to a wrapper </summary>
        private SignalRClientResponse ConvertSignalRResponse(string fullResponse)
        {
            var cleanedUpResponse = GetCleanedUpResponse(fullResponse);
            return JsonConvert.DeserializeObject<SignalRClientResponse>(cleanedUpResponse);
        }


        /// <summary> Insert the response to the request into the request, which will be found by id inside the response. </summary>
        private void SetResponseToSuitableRequest(SignalRClientResponse response)
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
