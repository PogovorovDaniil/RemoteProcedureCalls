using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Reflection;

namespace RemoteProcedureCalls
{
    public class RPCServer : IDisposable
    {
        private Socket socket;
        private Dictionary<Type, object> implementations;
        private Dictionary<string, Type> interfaces;
        private CancellationTokenSource tokenSource;
        private CancellationToken cancellationToken;

        public RPCServer()
        {
            implementations = new Dictionary<Type, object>();
            interfaces = new Dictionary<string, Type>();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, 55278));
            socket.Listen(0);

            tokenSource = new CancellationTokenSource();
            cancellationToken = tokenSource.Token;
            Task.Run(Listener, cancellationToken);
        }

        public void AddImplementation<TInterface>(TInterface implementation) where TInterface : class
        {
            if(implementations.Any(x => x.Key.Name == typeof(TInterface).Name)) throw new ArgumentException("Интерфейс с таким именем уже определён");
            interfaces[typeof(TInterface).Name] = typeof(TInterface);
            implementations[typeof(TInterface)] = implementation;
        }

        private async Task Listener()
        {
            while (!tokenSource.IsCancellationRequested)
            {
                Socket client = await socket.AcceptAsync(cancellationToken);
                await ClientCommunications(client);
            }
        }

        private async Task ClientCommunications(Socket client)
        {
            byte[] buffer;
            using (var stream = new NetworkStream(client))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    buffer = new byte[2];
                    if (await stream.ReadAsync(buffer, 0, buffer.Length) == 0) return;
                    int size = buffer[0] + 0x100 * buffer[1];
                    buffer = new byte[size];
                    if (await stream.ReadAsync(buffer, 0, buffer.Length) == 0) return;
                    string jsonCallObject = Encoding.UTF8.GetString(buffer, 0, size);
                    CallObject callObject = JsonSerializer.Deserialize<CallObject>(jsonCallObject);

                    object implementation = implementations[interfaces[callObject.InterfaceName]];
                    MethodInfo method = implementation.GetType().GetMethod(callObject.MethodName);
                    Type[] argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    object[] args = new object[callObject.Arguments.Length];
                    for (int i = 0; i < callObject.Arguments.Length; i++)
                    {
                        args[i] = JsonSerializer.Deserialize(callObject.Arguments[i], argTypes[i]);
                    }
                    object result = method.Invoke(implementation, args);

                    if (method.ReturnType == typeof(void)) return;
                    string resultJson = JsonSerializer.Serialize(result, method.ReturnType);
                    byte[] resultBytes = Encoding.UTF8.GetBytes(resultJson);
                    buffer = new byte[2];
                    buffer[0] = (byte)(resultBytes.Length & 0xFF);
                    buffer[1] = (byte)(resultBytes.Length / 0x100);
                    await stream.WriteAsync(buffer, cancellationToken);
                    await stream.WriteAsync(resultBytes, cancellationToken);
                }
            }
        }

        public void Dispose()
        {
            tokenSource.Cancel();
        }
    }
}
