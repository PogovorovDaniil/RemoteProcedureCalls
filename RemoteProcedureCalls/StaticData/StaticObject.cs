using System;
using System.Collections.Generic;

namespace RemoteProcedureCalls.StaticData
{
    internal static class StaticObject
    {
        private static Dictionary<Type, List<object>> objects = new Dictionary<Type, List<object>>();
        private static Dictionary<Type, object> locks = new Dictionary<Type, object>();

        public static int SaveObject(Type type, object obj)
        {
            if (!locks.ContainsKey(type)) locks.Add(type, new Dictionary<Type, object>());
            lock (locks[type])
            {
                if (!objects.ContainsKey(type)) objects.Add(type, new List<object>());
                int index = objects[type].IndexOf(obj);
                if (index > -1) return index;
                objects[type].Add(obj);
                return objects[type].Count - 1;
            }
        }
        public static int SaveObject<T>(T obj) => SaveObject(typeof(T), obj);
        public static object GetObject(Type type, int index)
        {
            if (!locks.ContainsKey(type)) locks.Add(type, new Dictionary<Type, object>());
            lock (locks[type])
            {
                return objects[type][index];
            }
        }
        public static T GetObject<T>(int index) => (T)GetObject(typeof(T), index);
    }
}
