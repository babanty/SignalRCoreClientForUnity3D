namespace SignalRCoreClientForUnity3D.Implementation
{
    internal class SignalRRequest
    {
        public string InvocationId { get; set; }
        public SignalRRequestStatus Status { get; set; }

        
        /// <summary> [CanBeNull] </summary>
        public SignalRClientResponse Response { get; set; }
    }
}
