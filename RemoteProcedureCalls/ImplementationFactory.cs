using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RemoteProcedureCalls
{
    public class ImplementationFactory
    {
        private static readonly AssemblyBuilder assembly;
        private static readonly ModuleBuilder module;
        private static readonly Dictionary<Type, TypeBuilder> definedTypes;
        private static readonly List<Type> returnTypes;
        private static string randomName => Guid.NewGuid().ToString();
        static ImplementationFactory()
        {
            assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(randomName), AssemblyBuilderAccess.Run);
            module = assembly.DefineDynamicModule(randomName);
            definedTypes = new Dictionary<Type, TypeBuilder>();
            returnTypes = new List<Type>();
        }

        public delegate object CallHandler(string interfaceName, string methodName, object[] parameters);
        private readonly CallHandler callHandler;
        public ImplementationFactory(CallHandler callHandler)
        {
            this.callHandler = callHandler;
        }

        private TypeBuilder CreateType<T>() where T : class
        {
            if (definedTypes.TryGetValue(typeof(T), out var builder)) return builder;

            TypeBuilder typeBuilder = module.DefineType(randomName);
            typeBuilder.AddInterfaceImplementation(typeof(T));

            FieldBuilder factoryField = typeBuilder.DefineField("factory", typeof(ImplementationFactory), FieldAttributes.Public);

            foreach (var method in typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                MethodBuilder methodBuilder = CreateMethod(method, typeBuilder, typeof(T), factoryField);
                typeBuilder.DefineMethodOverride(methodBuilder, method);
            }
            typeBuilder.CreateType();
            return typeBuilder;
        }

        private MethodBuilder CreateMethod(MethodInfo method, TypeBuilder typeBuilder, Type interfaceType, FieldBuilder factoryField)
        {
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual);
            ILGenerator il = methodBuilder.GetILGenerator();
            ParameterInfo[] parameterInfos = method.GetParameters();
            methodBuilder.SetParameters(parameterInfos.Select(x => x.ParameterType).ToArray());
            methodBuilder.SetReturnType(method.ReturnType);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, factoryField);

            il.Emit(OpCodes.Ldstr, interfaceType.Name);
            il.Emit(OpCodes.Ldstr, method.Name);

            il.Emit(OpCodes.Ldc_I4, parameterInfos.Length);
            il.Emit(OpCodes.Newarr, typeof(object));

            int paramIndex = 0;
            foreach (var parameter in parameterInfos)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, paramIndex);
                il.Emit(OpCodes.Ldarg, paramIndex + 1);
                paramIndex++;
                if (parameter.ParameterType.IsValueType)
                {
                    il.Emit(OpCodes.Box, parameter.ParameterType);
                }
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.EmitCall(OpCodes.Call, GetType().GetMethod(nameof(AnyMethod)), null);
            if (method.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
            else if (method.ReturnType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, method.ReturnType);
            }
            il.Emit(OpCodes.Ret);
            return methodBuilder;
        }

        public object AnyMethod(string interfaceName, string methodName, object[] parameters) => callHandler(interfaceName, methodName, parameters);
        public static Type GetReturnType(int index) => returnTypes[index];

        public T Create<T>() where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException();
            TypeBuilder type = CreateType<T>();
            T value = (T)assembly.CreateInstance(type.Name);
            type.GetField("factory").SetValue(value, this);
            return value;
        }
    }
}
