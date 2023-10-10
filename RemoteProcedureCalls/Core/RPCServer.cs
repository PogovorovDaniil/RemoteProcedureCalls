using RemoteProcedureCalls.Network;
using RemoteProcedureCalls.Network.Models;
using RemoteProcedureCalls.StaticData;
using RemoteProcedureCalls.StaticData.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace RemoteProcedureCalls.Core
{
    public class RPCServer : IDisposable
    {
        private readonly Server server;
        private readonly TypeFactory implementationFactory;
        private readonly Dictionary<Type, object> implementations;
        private readonly Dictionary<string, Type> interfaces;

        public RPCServer(int port = 55278)
        {
            implementations = new Dictionary<Type, object>();
            interfaces = new Dictionary<string, Type>();

            server = new Server($"0.0.0.0:{port}");
            server.OnClientConnected += Server_OnClientConnected;

            implementationFactory = new TypeFactory(null);
        }

        private void Server_OnClientConnected(ExtendedSocket socket)
        {
            Parallel.Invoke(() =>
            {
                while (!socket.IsClosed)
                {
                    CallObject callObject = socket.Receive<CallObject>(1);
                    object implementation = implementations[interfaces[callObject.InterfaceName]];
                    MethodInfo method = implementation.GetType().GetMethod(callObject.MethodName);
                    Type[] argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    object[] args = new object[callObject.Arguments.Length];
                    for (int i = 0; i < callObject.Arguments.Length; i++)
                    {
                        if (argTypes[i].IsAssignableTo(typeof(Delegate)))
                        {
                            int delegateIndex = JsonSerializer.Deserialize<int>(callObject.Arguments[i]);
                            int dataIndex = StaticDataService.SaveObject(new CallDelegateStaticData()
                            {
                                Socket = socket,
                                DelegateMethod = argTypes[i].GetMethod("Invoke"),
                                DelegateIndex = delegateIndex,
                                LockObject = new object()
                            });
                            args[i] = implementationFactory.CreateDelegate(argTypes[i], CallDelegate, dataIndex);
                        }
                        else
                        {
                            args[i] = JsonSerializer.Deserialize(callObject.Arguments[i], argTypes[i]);
                        }
                    }
                    object result = method.Invoke(implementation, args);

                    if (method.ReturnType == typeof(void)) continue;
                    if (result is Delegate)
                    {
                        int index = StaticDataService.SaveObject(typeof(Delegate), result);
                        socket.Send(index, 1);
                        continue;
                    }
                    socket.Send(result, method.ReturnType, 1);
                }
            }, () =>
            {
                while (!socket.IsClosed)
                {
                    CallDelegateObject callDelegateObject = socket.Receive<CallDelegateObject>(3);
                    Delegate @delegate = StaticDataService.GetObject<Delegate>(callDelegateObject.DelegateIndex);
                    MethodInfo method = @delegate.GetMethodInfo();
                    Type[] argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
                    object[] args = new object[callDelegateObject.Arguments.Length];
                    for (int i = 0; i < callDelegateObject.Arguments.Length; i++)
                    {
                        if (argTypes[i].IsAssignableTo(typeof(Delegate))) throw new NotSupportedException();
                        args[i] = JsonSerializer.Deserialize(callDelegateObject.Arguments[i], argTypes[i]);
                    }
                    object result = method.Invoke(@delegate.Target, args);
                    if (result is Delegate) throw new NotSupportedException();
                    if (method.ReturnType == typeof(void)) continue;
                    socket.Send(result, method.ReturnType, 3);
                }
            });
        }

        internal static object CallDelegate(string delegateName, int dataIndex, object[] parameters)
        {
            CallDelegateStaticData data = StaticDataService.GetObject<CallDelegateStaticData>(dataIndex);
            Type[] argTypes = data.DelegateMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            CallDelegateObject callDelegateObject = new CallDelegateObject()
            {
                DelegateName = delegateName,
                DelegateIndex = data.DelegateIndex,
                Arguments = new string[parameters.Length]
            };
            for (int i = 0; i < parameters.Length; i++)
            {
                if (argTypes[i].IsAssignableTo(typeof(Delegate))) throw new NotSupportedException();
                callDelegateObject.Arguments[i] = JsonSerializer.Serialize(parameters[i], argTypes[i]);
            }
            lock (data.LockObject)
            {
                data.Socket.Send(callDelegateObject, 2);
                if (data.DelegateMethod.ReturnType == typeof(void)) return null;
                else if (data.DelegateMethod.ReturnType.IsAssignableTo(typeof(Delegate)))
                {
                    throw new NotSupportedException();
                }
                else
                {
                    object result = data.Socket.Receive(data.DelegateMethod.ReturnType, 2);
                    return result;
                }
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

        ~RPCServer() => Dispose();
    }
}
