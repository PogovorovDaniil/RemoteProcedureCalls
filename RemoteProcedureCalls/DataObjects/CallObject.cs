namespace RemoteProcedureCalls.DataObjects
{
    public class CallObject
    {
        public string InterfaceName { get; set; }
        public string MethodName { get; set; }
        public string[] Arguments { get; set; }
    }
}
