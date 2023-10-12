using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RemoteProcedureCalls.Network
{
    internal class Server : IDisposable
    {
        private readonly Socket socket;
        private readonly IPEndPoint localEndPoint;
        private readonly Thread listenerThread;
        private readonly List<ExtendedSocket> clientPool;
        private readonly List<Thread> clientThreadPool;
        private readonly byte channelCount;
        private bool isDisposed;

        public event Action<ExtendedSocket> OnClientConnected;
        public Server(string address, byte channelCount = 16)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            localEndPoint = IPEndPoint.Parse(address);
            listenerThread = new Thread(SocketListener);
            clientPool = new List<ExtendedSocket>();
            clientThreadPool = new List<Thread>();
            this.channelCount = channelCount;
            isDisposed = false;

            listenerThread.Start();
        }
        private void SocketListener()
        {
            socket.Bind(localEndPoint);
            socket.Listen();
            try
            {
                while (!isDisposed)
                {
                    Socket clientSocket = socket.Accept();
                    ExtendedSocket client = new ExtendedSocket(clientSocket, channelCount);
                    clientPool.Add(client);
                    Thread clientLogic = new Thread(() => ClientLogic(client));
                    clientLogic.Start();
                    clientThreadPool.Add(clientLogic);
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.Interrupted) return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void ClientLogic(ExtendedSocket client)
        {
            try
            {
                OnClientConnected?.Invoke(client);
            }
            catch (ExtendedSocketClosedException) { }
        }

        public void Dispose()
        {
            foreach (ExtendedSocket es in clientPool) es.Dispose();
            foreach (Thread t in clientThreadPool) t.Join();

            isDisposed = true;
            socket.Close();

            listenerThread.Join();
        }

        ~Server() => Dispose();
    }
}
