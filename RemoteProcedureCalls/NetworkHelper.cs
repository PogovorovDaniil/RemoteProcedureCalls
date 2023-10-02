using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System;

namespace RemoteProcedureCalls
{
    public static class NetworkHelper
    {
        public static byte[] Read(NetworkStream stream)
        {
            try
            {
                byte[] buffer = new byte[2];
                if (stream.Read(buffer, 0, buffer.Length) == 0) return new byte[0];
                int size = buffer[0] + 0x100 * buffer[1];
                buffer = new byte[size];
                if (stream.Read(buffer, 0, buffer.Length) == 0) return new byte[0];
                return buffer;
            }
            catch 
            {  
                return new byte[0];
            }
        }
        public static T Read<T>(NetworkStream stream) => Deserialize<T>(Read(stream)); 
        public static object Read(NetworkStream stream, Type type) => Deserialize(Read(stream), type);
        public static bool Send(NetworkStream stream, byte[] data)
        {
            byte[] buffer = new byte[2];
            buffer[0] = (byte)(data.Length & 0xFF);
            buffer[1] = (byte)(data.Length / 0x100);
            try
            {
                stream.Write(buffer);
                stream.Write(data);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static bool Send<T>(NetworkStream stream, T data) => Send(stream, Serialize(data));
        public static bool Send(NetworkStream stream, object data, Type type) => Send(stream, Serialize(data, type));
        public static byte[] Serialize(object obj, Type type) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, type));
        public static byte[] Serialize<T>(T obj) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, typeof(T)));
        public static object Deserialize(byte[] data, Type type) => data.Length > 0 ? JsonSerializer.Deserialize(Encoding.UTF8.GetString(data, 0, data.Length), type) : default;
        public static T Deserialize<T>(byte[] data) => data.Length > 0 ? JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data, 0, data.Length)) : default;
    }
}
