﻿namespace RemoteProcedureCalls.Network.Models
{
    internal class CallDelegateObject
    {
        public string DelegateName { get; set; }
        public int DelegateIndex { get; set; }
        public string[] Arguments { get; set; }
    }
}