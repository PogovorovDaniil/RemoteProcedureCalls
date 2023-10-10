using RemoteProcedureCalls.Network;
using RemoteProcedureCalls.Network.Models;
using RemoteProcedureCalls.StaticData;
using RemoteProcedureCalls.StaticData.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace RemoteProcedureCalls.Core
{
    public class RPCClient : IDisposable
    {
        private readonly ExtendedSocket socket;
        private readonly TypeFactory implementationFactory;
        private readonly Dictionary<int, Type> interfaces;
        private readonly Dictionary<Type, MethodInfo[]> interfaceMethods;
        private readonly object lockObj;
        private readonly Thread callDelegateListener;
        public RPCClient(string address = "127.0.0.1", int port = 55278)
        {
            interfaces = new Dictionary<int, Type>();
            interfaceMethods = new Dictionary<Type, MethodInfo[]>();
            lockObj = new object();

            socket = new Client().Connect($"{address}:{port}");
            implementationFactory = new TypeFactory(CallMethod);

            callDelegateListener = new Thread(CallDelegateListener);
            callDelegateListener.Start();
        }

        public T GetImplementation<T>() where T : class
        {
            lock (lockObj)
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

        internal object CallMethod(int instanceIndex, int methodIndex, object[] parameters)
        {
            MethodInfo method = interfaceMethods[interfaces[instanceIndex]][methodIndex];
            Type[] argTypes = method.GetParameters().Select(p => p.ParameterType).ToArray();
            CallObject callObject = new CallObject()
            {
                InstanceIndex = instanceIndex,
                MethodIndex = methodIndex,
                Arguments = new string[parameters.Length]
            };
            for (int i = 0; i < parameters.Length; i++)
            {
                if (argTypes[i].IsAssignableTo(typeof(Delegate)))
                {
                    int index = StaticDataService.SaveObject(typeof(Delegate), parameters[i]);
                    callObject.Arguments[i] = JsonSerializer.Serialize(index, typeof(int));
                }
                else
                {
                    callObject.Arguments[i] = JsonSerializer.Serialize(parameters[i], argTypes[i]);
                }
            }
            lock (lockObj)
            {
                socket.Send(callObject, 1);
                if (method.ReturnType == typeof(void)) return null;
                else if (method.ReturnType.IsAssignableTo(typeof(Delegate)))
                {
                    int delegateIndex = socket.Receive<int>(1);
                    int dataIndex = StaticDataService.SaveObject(new CallDelegateStaticData()
                    {
                        DelegateIndex = delegateIndex,
                        DelegateMethod = method.ReturnType.GetMethod("Invoke"),
                        LockObject = new object(),
                        Socket = socket,
                    });
                    return implementationFactory.CreateDelegate(method.ReturnType, CallDelegate, dataIndex);
                }
                else
                {
                    object result = socket.Receive(method.ReturnType, 1);
                    return result;
                }
            }
        }

        internal static object CallDelegate(int dataIndex, object[] parameters) => ProtocolMethods.CallDelegate(dataIndex, 3, parameters);

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
