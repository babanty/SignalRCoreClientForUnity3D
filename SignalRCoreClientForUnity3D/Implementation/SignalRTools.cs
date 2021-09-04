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


        /// <summary> Получить телеграмму готовую к отправке на SignalREndpoint  </summary>
        /// <param name="request"> объект-контейнер содержащий аргументы запроса </param>
        /// <param name="method"> метод Hub-а SignalR-а </param>
        /// <param name="invocationId"> код с которым придет ответ на эту телеграмму </param>
        public static ArraySegment<byte> GetTelegramToSend<T>(T request, string method, string invocationId = null)
        {
            var arguments = new T[] { request };
            var serializedRequest = JsonConvert.SerializeObject(arguments);

            return GetStandardTelegram(method, serializedRequest, invocationId);
        }


        /// <summary> Получить телеграмму готовую к отправке на SignalREndpoint  </summary>
        /// <param name="args"> аргументы endpoint-а </param>
        /// <param name="method"> метод Hub-а SignalR-а </param>
        /// <param name="invocationId"> код с которым придет ответ на эту телеграмму </param>
        public static ArraySegment<byte> GetTelegramToSend(string method, object[] args, string invocationId = null)
        {
            var arguments = JsonConvert.SerializeObject(args);

            return GetStandardTelegram(method, arguments, invocationId);
        }


        /// <summary> Получить пустую телеграмму готовую к отправке на SignalREndpoint  </summary>
        /// <param name="method"> метод Hub-а SignalR-а </param>
        /// <param name="invocationId"> код с которым придет ответ на эту телеграмму </param>
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


        /// <param name="arguments"> должен быть json-массивом, например: [{}, "myStr"], в котором index элемента соответствует номеру аргумента в endpoint-е по счету </param>
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
