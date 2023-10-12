using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace RemoteProcedureCalls.Network
{
    internal class ExtendedSocket : IDisposable
    {
        private readonly Socket socket;
        private readonly BlockingQueue<byte[]>[] receiveData;
        private readonly Thread receiveHandler;
        private readonly object lockSend;
        internal bool IsClosed { get; private set; }

        internal ExtendedSocket(Socket socket, byte channelCount)
        {
            this.socket = socket;
            receiveData = new BlockingQueue<byte[]>[channelCount];
            for (int i = 0; i < receiveData.Length; i++) receiveData[i] = new BlockingQueue<byte[]>();
            receiveHandler = new Thread(ReceiveHandler);
            lockSend = new object();
            IsClosed = false;

            receiveHandler.Start();
        }

        private void ReceiveHandler()
        {
            using NetworkStream stream = new NetworkStream(socket);
            stream.ReadTimeout = 5000;
            while (!IsClosed)
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

                    receiveData[channel].Enqueue(buffer);
                }
                catch (IOException ex)
                {
                    if (ex.InnerException is SocketException socketException && socketException.SocketErrorCode == SocketError.TimedOut) continue;
                    Close();
                    break;
                }
            }
        }

        internal byte[] Receive(byte channel = 0)
        {
            if (IsClosed) throw new ExtendedSocketClosedException();

            if (receiveData[channel].TryDequeue(out byte[] data)) return data;
            throw new ExtendedSocketClosedException();
        }

        internal void Send(byte[] data, byte channel = 0)
        {
            if (IsClosed) throw new ExtendedSocketClosedException();

            lock (lockSend)
            {
                try
                {
                    using NetworkStream stream = new NetworkStream(socket);
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

        internal void WhileWorking(Action action)
        {
            try
            {
                while (!IsClosed)
                {
                    action();
                }
            }
            catch (ExtendedSocketClosedException) { }
        }

        private void Close()
        {
            IsClosed = true;
            foreach (var data in receiveData) data.Unlock();
            socket.Close();
        }
        public void Dispose()
        {
            Close();
            receiveHandler.Join();
        }
        ~ExtendedSocket() => Dispose();
    }
}
