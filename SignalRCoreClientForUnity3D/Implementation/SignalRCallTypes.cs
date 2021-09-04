namespace SignalRCoreClientForUnity3D.Implementation
{
    public enum SignalRCallTypes
    {
        Unknown = 0,
        /// <summary> Отправили простое сообщение </summary>
        Invocation = 1,
        StreamItem = 2,
        /// <summary> Завершение ответ на то что запрос обработан </summary>
        Completion = 3,
        StreamInvocation = 4,
        CancelInvocation = 5,
        Ping = 6,
        Close = 7
    }
}
