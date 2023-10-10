namespace RemoteProcedureCalls.Network.Models
{
    internal class CallObject
    {
        public int InstanceIndex { get; set; }
        public int MethodIndex { get; set; }
        public string[] Arguments { get; set; }
    }
}
