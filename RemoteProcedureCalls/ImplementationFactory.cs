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

        public delegate object CallHandler(string interfaceName, string methodName, object[] parameters, Type returnType);
        private readonly CallHandler callHandler;
        public ImplementationFactory(CallHandler callHandler)
        {
            this.callHandler = callHandler;
        }

        private TypeBuilder CreateType<T>() where T : class
        {
            if(definedTypes.TryGetValue(typeof(T), out var builder)) return builder;

            TypeBuilder typeBuilder = module.DefineType(randomName);
            typeBuilder.AddInterfaceImplementation(typeof(T));

            FieldBuilder fieldBuilder = typeBuilder.DefineField("factory", typeof(ImplementationFactory), FieldAttributes.Public);

            foreach(var method in typeof(T).GetMethods())
            {
                MethodBuilder methodBuilder = CreateMethod(method, typeBuilder, typeof(T), fieldBuilder);
                typeBuilder.DefineMethodOverride(methodBuilder, method);
            }
            typeBuilder.CreateType();
            return typeBuilder;
        }

        private MethodBuilder CreateMethod(MethodInfo method, TypeBuilder typeBuilder, Type interfaceType, FieldBuilder fieldBuilder)
        {
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual);
            ILGenerator il = methodBuilder.GetILGenerator();
            ParameterInfo[] parameterInfos = method.GetParameters();
            methodBuilder.SetParameters(parameterInfos.Select(x => x.ParameterType).ToArray());
            methodBuilder.SetReturnType(method.ReturnType);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fieldBuilder);

            il.Emit(OpCodes.Ldstr, interfaceType.Name);
            il.Emit(OpCodes.Ldstr, method.Name);

            il.Emit(OpCodes.Ldc_I4, parameterInfos.Length);
            il.Emit(OpCodes.Newarr, typeof(object));

            int paramIndex = 0;
            foreach(var parameter in parameterInfos)
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

            Type returnType = method.ReturnType;
            if(!returnTypes.Contains(returnType)) returnTypes.Add(returnType);
            int indexOfType = returnTypes.FindIndex(x => x == returnType);
            il.Emit(OpCodes.Ldc_I4, indexOfType);
            il.EmitCall(OpCodes.Call, GetType().GetMethod(nameof(GetReturnType)), null);

            il.EmitCall(OpCodes.Call, GetType().GetMethod(nameof(AnyMethod)), null);
            if (method.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Pop);
            }
            else if (method.ReturnType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, returnType);
            }
            il.Emit(OpCodes.Ret);
            return methodBuilder;
        }

        public object AnyMethod(string interfaceName, string methodName, object[] parameters, Type returnType) => callHandler(interfaceName, methodName, parameters, returnType);
        public static Type GetReturnType(int index) => returnTypes[index];

        public T Create<T>() where T : class
        {
            if(!typeof(T).IsInterface) throw new ArgumentException();
            TypeBuilder type = CreateType<T>();
            T value = (T)assembly.CreateInstance(type.Name);
            type.GetField("factory").SetValue(value, this);
            return value;
        }
    }
}
