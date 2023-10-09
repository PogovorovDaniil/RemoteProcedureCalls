using System;
using System.Text;
using System.Text.Json;

namespace RemoteProcedureCalls.Network
{
    public static class NetworkHelper
    {
        public static T Receive<T>(this ExtendedSocket socket, byte channel = 0) => Deserialize<T>(socket.Receive(channel));
        public static object Receive(this ExtendedSocket socket, Type type, byte channel = 0) => Deserialize(socket.Receive(channel), type);
        public static void Send<T>(this ExtendedSocket socket, T data, byte channel = 0) => socket.Send(Serialize(data), channel);
        public static void Send(this ExtendedSocket socket, object data, Type type, byte channel = 0) => socket.Send(Serialize(data, type), channel);
        public static byte[] Serialize(object obj, Type type) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, type));
        public static byte[] Serialize<T>(T obj) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, typeof(T)));
        public static object Deserialize(byte[] data, Type type) => data.Length > 0 ? JsonSerializer.Deserialize(Encoding.UTF8.GetString(data, 0, data.Length), type) : default;
        public static T Deserialize<T>(byte[] data) => data.Length > 0 ? JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data, 0, data.Length)) : default;
    }
}
