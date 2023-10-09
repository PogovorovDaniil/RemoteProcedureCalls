using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using RemoteProcedureCalls.Network;
using RemoteProcedureCalls.DataObjects;

namespace RemoteProcedureCalls
{
    public class RPCServer : IDisposable
    {
        private readonly Server server;
        private readonly Dictionary<Type, object> implementations;
        private readonly Dictionary<string, Type> interfaces;

        public RPCServer(int port = 55278)
        {
            implementations = new Dictionary<Type, object>();
            interfaces = new Dictionary<string, Type>();

            server = new Server($"0.0.0.0:{port}");
            server.OnClientConnected += Server_OnClientConnected;
        }

        private void Server_OnClientConnected(ExtendedSocket socket)
        {
            while (!socket.IsDisposed)
            {
                CallObject callObject = socket.Receive<CallObject>();
                object implementation = implementations[interfaces[callObject.InterfaceName]];
                MethodInfo method = implementation.GetType().GetMethod(callObject.MethodName);
                Type[] argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                object[] args = new object[callObject.Arguments.Length];
                for (int i = 0; i < callObject.Arguments.Length; i++)
                {
                    args[i] = JsonSerializer.Deserialize(callObject.Arguments[i], argTypes[i]);
                }
                object result = method.Invoke(implementation, args);
                if (method.ReturnType == typeof(void)) continue;
                socket.Send(result, method.ReturnType);
            }
        }

        public void AddImplementation<TInterface>(TInterface implementation) where TInterface : class
        {
            if (implementations.Any(x => x.Key.Name == typeof(TInterface).Name)) throw new ArgumentException("Интерфейс с таким именем уже определён");
            interfaces[typeof(TInterface).Name] = typeof(TInterface);
            implementations[typeof(TInterface)] = implementation;
        }

        public void Dispose()
        {
            server.Dispose();
        }
    }
}
