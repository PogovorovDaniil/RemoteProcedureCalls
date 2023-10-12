using RemoteProcedureCalls.Network;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace RemoteProcedureCalls.Core
{
    public class RPCClient : IDisposable
    {
        private readonly ExtendedSocket socket;
        private readonly TypeFactory implementationFactory;
        private readonly Dictionary<int, Type> interfaces;
        private readonly Dictionary<Type, MethodInfo[]> interfaceMethods;
        private readonly object lockObject;
        private readonly Thread callDelegateListener;
        public RPCClient(string address = "127.0.0.1", int port = 55278)
        {
            interfaces = new Dictionary<int, Type>();
            interfaceMethods = new Dictionary<Type, MethodInfo[]>();
            lockObject = new object();

            socket = new Client().Connect($"{address}:{port}");
            implementationFactory = new TypeFactory(CallMethod);

            callDelegateListener = new Thread(CallDelegateListener);
            callDelegateListener.Start();
        }

        public T GetImplementation<T>() where T : class
        {
            lock (lockObject)
            {
                if (!typeof(T).IsInterface) throw new ArgumentException();
                socket.Send(typeof(T).Name, 0);
                int index = socket.Receive<int>(0);
                if (index < 0) throw new NotImplementedException();
                if (!interfaces.ContainsKey(index))
                {
                    interfaces.Add(index, typeof(T));
                    if (!interfaceMethods.ContainsKey(typeof(T)))
                    {
                        interfaceMethods[typeof(T)] = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                }
                return implementationFactory.Create<T>(index);
            }
        }

        internal object CallMethod(int instanceIndex, int methodIndex, object[] parameters) => ProtocolMethods.CallMethod(socket, implementationFactory, CallDelegate, interfaces, interfaceMethods, lockObject, instanceIndex, methodIndex, parameters, 1);

        internal static object CallDelegate(int dataIndex, object[] parameters) => ProtocolMethods.CallDelegate(dataIndex, parameters, 3);

        private void CallDelegateListener()
        {
            ProtocolMethods.CallDelegateHandler(socket, 2);
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        ~RPCClient() => Dispose();
    }
}
