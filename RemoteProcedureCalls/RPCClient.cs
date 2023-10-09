using System;
using System.Reflection;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using RemoteProcedureCalls.Network;
using RemoteProcedureCalls.DataObjects;

namespace RemoteProcedureCalls
{
    public class RPCClient : IDisposable
    {
        private readonly ExtendedSocket socket;
        private readonly ImplementationFactory implementationFactory;
        private readonly Dictionary<string, Type> interfaces;
        private readonly object lockObj;
        public RPCClient(string address = "127.0.0.1", int port = 55278)
        {
            interfaces = new Dictionary<string, Type>();
            lockObj = new object();

            socket = new Client().Connect($"{address}:{port}");
            implementationFactory = new ImplementationFactory((interfaceName, methodName, parameters) => Call(interfaceName, methodName, parameters));
        }

        public T GetImplementation<T>() where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException();
            interfaces.Add(typeof(T).Name, typeof(T));
            return implementationFactory.Create<T>();
        }

        public object Call(string interfaceName, string methodName, object[] parameters)
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
                socket.Send(callObject);
                if(method.ReturnType == typeof(void)) return null;
                object result = socket.Receive(method.ReturnType);
                return result;
            }
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
