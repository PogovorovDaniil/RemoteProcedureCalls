using System.Net;
using System.Net.Sockets;

namespace RemoteProcedureCalls.Network
{
    internal class Client
    {
        private readonly Socket socket;
        private readonly byte channelCount;

        public Client(byte channelCount = 16)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.channelCount = channelCount;
        }

        public ExtendedSocket Connect(string address)
        {
            socket.Connect(IPEndPoint.Parse(address));
            return new ExtendedSocket(socket, channelCount);
        }
    }
}
