using RemoteProcedureCalls.Network;
using System.Reflection;

namespace RemoteProcedureCalls.StaticData.Models
{
    internal class CallDelegateStaticData
    {
        public ExtendedSocket Socket { get; set; }
        public MethodInfo DelegateMethod { get; set; }
        public int DelegateIndex { get; set; }
        public object LockObject { get; set; }
    }
}
