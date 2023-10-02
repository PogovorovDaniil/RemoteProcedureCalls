using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

namespace RemoteProcedureCalls
{
    public class RPCServer : IDisposable
    {
        private readonly Socket socket;
        private readonly Dictionary<Type, object> implementations;
        private readonly Dictionary<string, Type> interfaces;

        private readonly CancellationTokenSource tokenSource;
        private readonly CancellationToken cancellationToken;

        public RPCServer(int port = 55278)
        {
            implementations = new Dictionary<Type, object>();
            interfaces = new Dictionary<string, Type>();

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            socket.Listen(0);

            tokenSource = new CancellationTokenSource();
            cancellationToken = tokenSource.Token;
            Task.Run(Listener, cancellationToken);
        }

        public void AddImplementation<TInterface>(TInterface implementation) where TInterface : class
        {
            if (implementations.Any(x => x.Key.Name == typeof(TInterface).Name)) throw new ArgumentException("Интерфейс с таким именем уже определён");
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
            using (var stream = new NetworkStream(client))
            {
                Console.WriteLine("{0} connected", client.RemoteEndPoint);
                while (!cancellationToken.IsCancellationRequested || client.Connected)
                {
                    Console.WriteLine("{0} ping", client.RemoteEndPoint);
                    CallObject callObject = await NetworkHelper.ReadAsync<CallObject>(stream, cancellationToken);
                    if (callObject is null) break;

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
                    await NetworkHelper.SendAsync(stream, result, method.ReturnType, cancellationToken);
                }
                Console.WriteLine("{0} disconnected", client.RemoteEndPoint);
            }
        }

        public void Dispose() 
        {
            tokenSource.Cancel();
            socket.Dispose();
        }
    }
}
