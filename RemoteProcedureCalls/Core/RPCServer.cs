using RemoteProcedureCalls.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace RemoteProcedureCalls.Core
{
    public class RPCServer : IDisposable
    {
        private readonly Server server;
        private readonly TypeFactory implementationFactory;
        private readonly Dictionary<Type, Func<object>> implementations;
        private readonly Dictionary<string, Type> interfaceNames;
        private readonly Dictionary<int, Type> interfaces;
        private readonly Dictionary<Type, MethodInfo[]> interfaceMethods;
        private readonly List<object> instances;

        public RPCServer(int port = 55278)
        {
            implementations = new Dictionary<Type, Func<object>>();
            interfaceNames = new Dictionary<string, Type>();
            interfaces = new Dictionary<int, Type>();
            interfaceMethods = new Dictionary<Type, MethodInfo[]>();
            instances = new List<object>();

            server = new Server($"0.0.0.0:{port}");
            server.OnClientConnected += Server_OnClientConnected;

            implementationFactory = new TypeFactory(null);
        }

        private void Server_OnClientConnected(ExtendedSocket socket)
        {
            try
            {
                Parallel.Invoke(
                    () => ProtocolMethods.GetImplementation(socket, interfaceNames, implementations, interfaces, interfaceMethods, instances, 0),
                    () => ProtocolMethods.CallMethodHandler(socket, implementationFactory, CallDelegate, interfaces, interfaceMethods, instances, 1),
                    () => ProtocolMethods.CallDelegateHandler(socket, 3));
            }
            catch (Exception ex)
            {

            }
        }

        internal static object CallDelegate(int dataIndex, object[] parameters) => ProtocolMethods.CallDelegate(dataIndex, 2, parameters);

        public void AddImplementation<TInterface>(Func<object> implementation) where TInterface : class
        {
            if (implementations.Any(x => x.Key.Name == typeof(TInterface).Name)) throw new ArgumentException("Интерфейс с таким именем уже определён");
            interfaceNames[typeof(TInterface).Name] = typeof(TInterface);
            implementations[typeof(TInterface)] = implementation;
        }

        public void Dispose()
        {
            server.Dispose();
        }

        ~RPCServer() => Dispose();
    }
}
