using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace RemoteProcedureCalls.Core
{
    // TODO IEnumerable
    public class TypeFactory
    {
        private readonly AssemblyBuilder assembly;
        private readonly ModuleBuilder module;
        private readonly Dictionary<Type, TypeBuilder> definedTypes;
        private readonly List<Type> returnTypes;
        private static string RandomName => Guid.NewGuid().ToString();

        public delegate object MethodHandler(int instanceIndex, int methodIndex, object[] parameters);
        private readonly MethodHandler methodHandler;

        public delegate object DelegateHandler(int dataIndex, object[] parameters);
        public TypeFactory(MethodHandler methodHandler)
        {
            assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(RandomName), AssemblyBuilderAccess.Run);
            module = assembly.DefineDynamicModule(RandomName);
            definedTypes = new Dictionary<Type, TypeBuilder>();
            returnTypes = new List<Type>();
            this.methodHandler = methodHandler;
        }
        private TypeBuilder CreateType<T>(int instanceIndex) where T : class
        {
            if (definedTypes.TryGetValue(typeof(T), out var builder)) return builder;

            TypeBuilder typeBuilder = module.DefineType(RandomName);
            typeBuilder.AddInterfaceImplementation(typeof(T));

            FieldBuilder factoryField = typeBuilder.DefineField("factory", typeof(TypeFactory), FieldAttributes.Public);

            int methodIndex = 0;
            foreach (var method in typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                MethodBuilder methodBuilder = CreateMethod(method, typeBuilder, factoryField, instanceIndex, methodIndex);
                typeBuilder.DefineMethodOverride(methodBuilder, method);
                methodIndex++;
            }
            typeBuilder.CreateType();
            return typeBuilder;
        }
        private MethodBuilder CreateMethod(MethodInfo method, TypeBuilder typeBuilder, FieldBuilder factoryField, int instanceIndex, int methodIndex)
        {
            MethodBuilder methodBuilder = typeBuilder.DefineMethod(method.Name, MethodAttributes.Public | MethodAttributes.Virtual);
            ILGenerator il = methodBuilder.GetILGenerator();
            ParameterInfo[] parameterInfos = method.GetParameters();
            methodBuilder.SetParameters(parameterInfos.Select(x => x.ParameterType).ToArray());
            methodBuilder.SetReturnType(method.ReturnType);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, factoryField);

            il.Emit(OpCodes.Ldc_I4, instanceIndex);

            il.Emit(OpCodes.Ldc_I4, methodIndex);

            il.Emit(OpCodes.Ldc_I4, parameterInfos.Length);
            il.Emit(OpCodes.Newarr, typeof(object));

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (parameterInfos[i].ParameterType.IsValueType)
                {
                    il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);
                }
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.EmitCall(OpCodes.Call, GetType().GetMethod(nameof(MethodInvoke)), null);
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
        public object MethodInvoke(int instanceIndex, int methodIndex, object[] parameters) => methodHandler(instanceIndex, methodIndex, parameters);

        public T Create<T>(int instanceIndex) where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException();
            TypeBuilder type = CreateType<T>(instanceIndex);
            T value = (T)assembly.CreateInstance(type.Name);
            type.GetField("factory").SetValue(value, this);
            return value;
        }
        public object CreateDelegate(Type type, DelegateHandler handler, int dataIndex)
        {
            var methodInfo = type.GetMethod("Invoke");
            var parameterInfos = methodInfo.GetParameters();

            var method = new DynamicMethod(RandomName, methodInfo.ReturnType, parameterInfos.Select(x => x.ParameterType).ToArray());
            var il = method.GetILGenerator();

            il.Emit(OpCodes.Ldc_I4, dataIndex);

            il.Emit(OpCodes.Ldc_I4, parameterInfos.Length);
            il.Emit(OpCodes.Newarr, typeof(object));

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i);
                if (parameterInfos[i].ParameterType.IsValueType) il.Emit(OpCodes.Box, parameterInfos[i].ParameterType);
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.EmitCall(OpCodes.Call, handler.Method, null);
            if (methodInfo.ReturnType == typeof(void)) il.Emit(OpCodes.Pop);
            else if (methodInfo.ReturnType.IsValueType) il.Emit(OpCodes.Unbox_Any, methodInfo.ReturnType);
            il.Emit(OpCodes.Ret);

            return method.CreateDelegate(type);
        }
    }
}
