using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace RemoteProcedureCalls
{
    public class RPCClient
    {
        private Socket socket;
        private ImplementationFactory implementationFactory;
        private Dictionary<string, Type> interfaces;
        public RPCClient(string address = "127.0.0.1")
        {
            interfaces = new Dictionary<string, Type>();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(new IPEndPoint(IPAddress.Parse(address), 55278));
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
            byte[] callBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(callObject));
            using (var stream = new NetworkStream(socket))
            {
                byte[] buffer = new byte[2];
                buffer[0] = (byte)(callBytes.Length & 0xFF);
                buffer[1] = (byte)(callBytes.Length / 0x100);
                stream.Write(buffer);
                stream.Write(callBytes);

                buffer = new byte[2];
                stream.Read(buffer); 
                int size = buffer[0] + 0x100 * buffer[1];
                buffer = new byte[size];
                stream.Read(buffer);

                string resultJson = Encoding.UTF8.GetString(buffer, 0, size);
                object result = JsonSerializer.Deserialize(resultJson, returnType);
                return result;
            }
        }
    }
}
