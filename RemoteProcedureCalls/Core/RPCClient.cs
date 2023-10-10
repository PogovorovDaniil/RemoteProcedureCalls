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
        private readonly Dictionary<string, Type> interfaces;
        private readonly object lockObj;
        private readonly Thread callDelegateListener;
        public RPCClient(string address = "127.0.0.1", int port = 55278)
        {
            interfaces = new Dictionary<string, Type>();
            lockObj = new object();

            socket = new Client().Connect($"{address}:{port}");
            implementationFactory = new TypeFactory(
                (interfaceName, methodName, parameters) => Call(interfaceName, methodName, parameters));

            callDelegateListener = new Thread(CallDelegateListener);
            callDelegateListener.Start();
        }

        public T GetImplementation<T>() where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException();
            interfaces.Add(typeof(T).Name, typeof(T));
            return implementationFactory.Create<T>();
        }

        internal object Call(string interfaceName, string methodName, object[] parameters)
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
                data.Socket.Send(callDelegateObject, 3);
                if (data.DelegateMethod.ReturnType == typeof(void)) return null;
                else if (data.DelegateMethod.ReturnType.IsAssignableTo(typeof(Delegate)))
                {
                    throw new NotSupportedException();
                }
                else
                {
                    object result = data.Socket.Receive(data.DelegateMethod.ReturnType, 3);
                    return result;
                }
            }
        }

        private void CallDelegateListener()
        {
            while (!socket.IsClosed)
            {
                CallDelegateObject callDelegateObject = socket.Receive<CallDelegateObject>(2);
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
                socket.Send(result, method.ReturnType, 2);
            }
        }

        public void Dispose()
        {
            socket.Dispose();
        }

        ~RPCClient() => Dispose();
    }
}
