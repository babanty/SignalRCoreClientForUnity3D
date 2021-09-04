using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalRCoreClientForUnity3D.Implementation
{
    internal static class SignalRTools
    {
        /// <summary> end of every package is a Separator, the separator is 0x1e for Json Protocol. </summary>
        private const byte TelegramSeparator = 0x1e;
        /// <summary> end of every package is a Separator, the separator is 0x1e for Json Protocol. </summary>
        public const char TelegramSeparatorChar = '\u001e';


        /// <summary> Get a telegram ready to be sent to SignalREndpoint </summary>
        /// <param name="request"> a container object which contains the request arguments </param>
        /// <param name="method"> SignalR Hub method </param>
        /// <param name="invocationId"> telegram id from response </param>
        public static ArraySegment<byte> GetTelegramToSend<T>(T request, string method, string invocationId = null)
        {
            var arguments = new T[] { request };
            var serializedRequest = JsonConvert.SerializeObject(arguments);

            return GetStandardTelegram(method, serializedRequest, invocationId);
        }


        /// <summary> Get a telegram ready to be sent to SignalREndpoint </summary>
        /// <param name="args"> endpoint arguments </param>
        /// <param name="method"> SignalR Hub method  </param>
        /// <param name="invocationId"> telegram id from response </param>
        public static ArraySegment<byte> GetTelegramToSend(string method, object[] args, string invocationId = null)
        {
            var arguments = JsonConvert.SerializeObject(args);

            return GetStandardTelegram(method, arguments, invocationId);
        }


        /// <summary> Get an empty telegram ready to be sent to SignalREndpoint </summary>
        /// <param name="method"> SignalR Hub method </param>
        /// <param name="invocationId"> telegram id from response</param>
        public static ArraySegment<byte> GetTelegramToSend(string method, string invocationId = null)
        {
            return GetStandardTelegram(method, "[]", invocationId);
        }


        public static ArraySegment<byte> EncodeTelegram(string telegram)
        {
            var byteMessage = new List<byte>(Encoding.UTF8.GetBytes(telegram)) { TelegramSeparator };

            return new ArraySegment<byte>(byteMessage.ToArray());
        }


        public static string DecodeTelegram(ArraySegment<byte> telegram)
        {
            return Encoding.UTF8.GetString(telegram.Array);
        }


        public static T ConvertArgument<T>(object argument)
        {
            if(typeof(T) == typeof(bool) || typeof(T) == typeof(int) || typeof(T) == typeof(string))
                return (T)Convert.ChangeType(argument, typeof(T));

            if (argument is JObject)
                return JsonConvert.DeserializeObject<T>((argument as JObject).ToString());

            return JsonConvert.DeserializeObject<T>(argument.ToString());
        }


        /// <param name="arguments"> Must be a json array, for example: [{}, "myStr"], in which the index of the element corresponds to the number of the argument in the endpoint by count </param>
        private static ArraySegment<byte> GetStandardTelegram(string method, string arguments = "" , string invocationId = null)
        {
            if (arguments[0] != '[' || arguments[arguments.Length - 1] != ']')
                throw new ArgumentException($"{nameof(arguments)} must be json-array");

            if (string.IsNullOrEmpty(invocationId))
                invocationId = Guid.NewGuid().ToString();

            var telegram = $"{{\"type\": 1, \"invocationId\": \"{invocationId}\", \"target\": \"{method}\", \"arguments\": {arguments}}}";

            return EncodeTelegram(telegram);
        }
    }
}
