using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;

namespace RemoteProcedureCalls
{
    public class RPCClient : IDisposable
    {
        private readonly Socket socket;
        private readonly ImplementationFactory implementationFactory;
        private readonly Dictionary<string, Type> interfaces;
        private readonly object lockObj;
        public RPCClient(string address = "127.0.0.1", int port = 55278)
        {
            interfaces = new Dictionary<string, Type>();
            lockObj = new object();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(IPAddress.Parse(address), port));
            implementationFactory = new ImplementationFactory((interfaceName, methodName, parameters, returnType) => Call(interfaceName, methodName, parameters, returnType));
        }

        public T GetImplementation<T>() where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException();
            interfaces.Add(typeof(T).Name, typeof(T));
            return implementationFactory.Create<T>();
        }

        public object Call(string interfaceName, string methodName, object[] parameters, Type returnType)
        {
            MethodInfo method = interfaces[interfaceName].GetMethod(methodName);
            Type[] argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
            CallObject callObject = new CallObject()
            {
                InterfaceName = interfaceName,
                MethodName = methodName,
                Arguments = new string[parameters.Length]
            };
            for (int i = 0; i < parameters.Length; i++)
            {
                callObject.Arguments[i] = JsonSerializer.Serialize(parameters[i], argTypes[i]);
            }
            lock (lockObj)
            {
                using (var stream = new NetworkStream(socket))
                {
                    NetworkHelper.Send(stream, callObject);
                    if(returnType == typeof(void)) return null;
                    return NetworkHelper.Read(stream, returnType);
                }
            }
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
