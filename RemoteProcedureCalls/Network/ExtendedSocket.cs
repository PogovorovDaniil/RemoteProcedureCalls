using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace RemoteProcedureCalls.Network
{
    internal class ExtendedSocket : IDisposable
    {
        private readonly Socket socket;
        private readonly BlockingQueue<byte[]>[] inputData;
        private readonly Thread receiveThread;
        private readonly object lockSend;
        private bool isDisposed;

        private bool isThrowed;
        private Exception throwException;

        public bool IsClosed => isDisposed || isThrowed;
        public int ReceiveTimeout { get; set; } = -1;
        public int SendTimeout { get; set; } = -1;

        internal ExtendedSocket(Socket socket, byte channelCount)
        {
            this.socket = socket;
            inputData = new BlockingQueue<byte[]>[channelCount];
            for (int i = 0; i < inputData.Length; i++) inputData[i] = new BlockingQueue<byte[]>();
            receiveThread = new Thread(ReceiveHandler);
            lockSend = new object();
            isDisposed = false;

            isThrowed = false;
            throwException = null;

            receiveThread.Start();
        }

        private void ReceiveHandler()
        {
            using NetworkStream stream = new NetworkStream(socket);
            stream.ReadTimeout = 1000;
            while (!isDisposed)
            {
                try
                {
                    byte[] buffer;
                    buffer = new byte[1];
                    stream.Read(buffer);
                    byte channel = buffer[0];
                    buffer = new byte[4];
                    stream.Read(buffer);
                    int size =
                        buffer[0] +
                        buffer[1] * 0x100 +
                        buffer[2] * 0x10000 +
                        buffer[3] * 0x1000000;
                    buffer = new byte[size];
                    stream.Read(buffer);

                    inputData[channel].Enqueue(buffer);
                }
                catch (IOException ex)
                {
                    if (ex.InnerException is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut) continue;

                    throwException = ex.InnerException ?? ex;
                    isThrowed = true;
                    break;
                }
            }
            Dispose();
        }

        public byte[] Receive(byte channel = 0)
        {
            if (isThrowed) throw throwException;
            if (IsClosed) throw new ObjectDisposedException(GetType().FullName);
            if (inputData[channel].TryDequeue(out byte[] data)) return data;
            throw new SocketException((int)SocketError.TimedOut);
        }

        public void Send(byte[] data, byte channel = 0)
        {
            if (IsClosed) throw new ObjectDisposedException(GetType().FullName);

            lock (lockSend)
            {
                try
                {
                    using NetworkStream stream = new NetworkStream(socket);
                    stream.WriteTimeout = SendTimeout;
                    byte[] buffer;
                    buffer = new byte[] { channel };
                    stream.Write(buffer);
                    buffer = new byte[]
                    {
                        (byte)(data.Length & 0xFF),
                        (byte)((data.Length / 0x100) & 0xFF),
                        (byte)((data.Length / 0x10000) & 0xFF),
                        (byte)((data.Length / 0x1000000) & 0xFF)
                    };
                    stream.Write(buffer);
                    stream.Write(data);
                }
                catch (Exception ex)
                {
                    throw ex.InnerException ?? ex;
                }
            }
        }

        public void Dispose()
        {
            isDisposed = false;
            foreach (var data in inputData)
            {
                data.Unlock();
            }
            socket.Close();
            receiveThread.Join();
        }
    }
}
