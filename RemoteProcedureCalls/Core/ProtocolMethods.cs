using RemoteProcedureCalls.Network;
using RemoteProcedureCalls.Network.Models;
using RemoteProcedureCalls.StaticData;
using RemoteProcedureCalls.StaticData.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace RemoteProcedureCalls.Core
{
    internal static class ProtocolMethods
    {
        internal static void GetImplementation(
            ExtendedSocket socket, 
            Dictionary<string, Type> interfaceNames, 
            Dictionary<Type, Func<object>> implementations,
            Dictionary<int, Type> interfaces,
            Dictionary<Type, MethodInfo[]> interfaceMethods,
            List<object> instances,
            byte channel)
        {
            while (!socket.IsClosed)
            {
                string interfaceName = socket.Receive<string>(channel);
                if (!interfaceNames.ContainsKey(interfaceName))
                {
                    socket.Send(-1, channel);
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
                    socket.Send(index, channel);
                    continue;
                }
                socket.Send(instances.Count, channel);
                instances.Add(instance);
            }
        }
        internal static void CallMethodHandler(
            ExtendedSocket socket,
            TypeFactory implementationFactory,
            TypeFactory.DelegateHandler callMethodDelegate,
            Dictionary<int, Type> interfaces,
            Dictionary<Type, MethodInfo[]> interfaceMethods,
            List<object> instances,
            byte channel)
        {
            while (!socket.IsClosed)
            {
                CallObject callObject = socket.Receive<CallObject>(channel);
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
                        args[i] = implementationFactory.CreateDelegate(argTypes[i], callMethodDelegate, dataIndex);
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
                    socket.Send(index, channel);
                    continue;
                }
                socket.Send(result, method.ReturnType, channel);
            }
        }
        internal static void CallDelegateHandler(ExtendedSocket socket, byte channel)
        {
            while (!socket.IsClosed)
            {
                CallDelegateObject callDelegateObject = socket.Receive<CallDelegateObject>(channel);
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
                if (method.ReturnType == typeof(void)) socket.Send(1, channel);
                else socket.Send(result, method.ReturnType, channel);
            }
        }
        internal static object CallDelegate(int dataIndex, byte channel, object[] parameters)
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
                data.Socket.Send(callDelegateObject, channel);
                if (data.DelegateMethod.ReturnType == typeof(void))
                {
                    data.Socket.Receive<int>(channel);
                    return null;
                }
                else if (data.DelegateMethod.ReturnType.IsAssignableTo(typeof(Delegate)))
                {
                    throw new NotSupportedException();
                }
                else
                {
                    object result = data.Socket.Receive(data.DelegateMethod.ReturnType, channel);
                    return result;
                }
            }
        }
    }
}
