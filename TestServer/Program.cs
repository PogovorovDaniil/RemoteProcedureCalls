using RemoteProcedureCalls;
using System;

namespace TestServer
{
    public interface IMath
    {
        int Sum(int a, int b);
        int Mul(int a, int b);
        void Print(string text);
    }
    public class Math : IMath
    {
        public int Sum(int a, int b) => a + b;
        public int Mul(int a, int b) => a * b;
        public void Print(string text) => Console.WriteLine(text);
    }
    internal class Program
    {
        static void Main(string[] args)
        {
            var server = new RPCServer();
            server.AddImplementation<IMath>(new Math());
            while (true) ;
        }
    }
}