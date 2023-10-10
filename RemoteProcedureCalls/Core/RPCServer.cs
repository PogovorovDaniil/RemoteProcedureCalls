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
            Parallel.Invoke(() =>
            {
                while (!socket.IsClosed)
                {
                    string interfaceName = socket.Receive<string>(0);
                    if(!interfaceNames.ContainsKey(interfaceName))
                    {
                        socket.Send(-1, 0);
                        continue;
                    }
                    object instance = implementations[interfaceNames[interfaceName]]();
                    int index = instances.IndexOf(instance);
                    interfaces[index < 0 ? instances.Count : index] = interfaceNames[interfaceName];
                    if (!interfaceMethods.ContainsKey(interfaceNames[interfaceName]))
                    {
                        interfaceMethods[interfaceNames[interfaceName]] = interfaceNames[interfaceName].GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    if (index >= 0)
                    {
                        socket.Send(index, 0);
                        continue;
                    }
                    socket.Send(instances.Count, 0);
                    instances.Add(instance);
                }
            }, () =>
            {
                while (!socket.IsClosed)
                {
                    CallObject callObject = socket.Receive<CallObject>(1);
                    object implementation = instances[callObject.InstanceIndex];
                    MethodInfo method = interfaceMethods[interfaces[callObject.InstanceIndex]][callObject.MethodIndex];
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
                    if (method.ReturnType == typeof(void)) socket.Send(1, 3);
                    else socket.Send(result, method.ReturnType, 3);
                }
            });
        }

        internal static object CallDelegate(int dataIndex, object[] parameters)
        {
            CallDelegateStaticData data = StaticDataService.GetObject<CallDelegateStaticData>(dataIndex);
            Type[] argTypes = data.DelegateMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            CallDelegateObject callDelegateObject = new CallDelegateObject()
            {
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
                if (data.DelegateMethod.ReturnType == typeof(void))
                {
                    data.Socket.Receive<int>(2);
                    return null;
                }
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
