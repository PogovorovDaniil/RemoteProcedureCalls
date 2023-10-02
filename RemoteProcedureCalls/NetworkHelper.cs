using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace RemoteProcedureCalls
{
    public static class NetworkHelper
    {
        public static async Task<byte[]> ReadAsync(NetworkStream stream, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[2];
            if (await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken) == 0) return new byte[0];
            int size = buffer[0] + 0x100 * buffer[1];
            buffer = new byte[size];
            if (await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken) == 0) return new byte[0];
            return buffer;
        }
        public static async Task SendAsync(NetworkStream stream, byte[] data, CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[2];
            buffer[0] = (byte)(data.Length & 0xFF);
            buffer[1] = (byte)(data.Length / 0x100);
            await stream.WriteAsync(buffer, cancellationToken);
            await stream.WriteAsync(data, cancellationToken);
        }
        public static byte[] Read(NetworkStream stream)
        {
            byte[] buffer = new byte[2];
            if (stream.Read(buffer, 0, buffer.Length) == 0) return new byte[0];
            int size = buffer[0] + 0x100 * buffer[1];
            buffer = new byte[size];
            if (stream.Read(buffer, 0, buffer.Length) == 0) return new byte[0];
            return buffer;
        }
        public static void Send(NetworkStream stream, byte[] data)
        {
            byte[] buffer = new byte[2];
            buffer[0] = (byte)(data.Length & 0xFF);
            buffer[1] = (byte)(data.Length / 0x100);
            stream.Write(buffer);
            stream.Write(data);
        }
        public static async Task<T> ReadAsync<T>(NetworkStream stream, CancellationToken cancellationToken = default) =>
            Deserialize<T>(await ReadAsync(stream, cancellationToken));
        public static async Task<object> ReadAsync(NetworkStream stream, Type type, CancellationToken cancellationToken = default) =>
            Deserialize(await ReadAsync(stream, cancellationToken), type);
        public static async Task SendAsync<T>(NetworkStream stream, T data, CancellationToken cancellationToken = default) =>
            await SendAsync(stream, Serialize(data), cancellationToken);
        public static async Task SendAsync(NetworkStream stream, object data, Type type, CancellationToken cancellationToken = default) =>
            await SendAsync(stream, Serialize(data, type), cancellationToken);
        public static T Read<T>(NetworkStream stream) => Deserialize<T>(Read(stream)); 
        public static object Read(NetworkStream stream, Type type) => Deserialize(Read(stream), type);
        public static void Send<T>(NetworkStream stream, T data) => Send(stream, Serialize(data));
        public static void Send(NetworkStream stream, object data, Type type) => Send(stream, Serialize(data, type));
        public static byte[] Serialize(object obj, Type type) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, type));
        public static byte[] Serialize<T>(T obj) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj, typeof(T)));
        public static object Deserialize(byte[] data, Type type) => JsonSerializer.Deserialize(Encoding.UTF8.GetString(data, 0, data.Length), type);
        public static T Deserialize<T>(byte[] data) => JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(data, 0, data.Length));
    }
}
