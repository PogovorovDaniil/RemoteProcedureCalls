using RemoteProcedureCalls;
using System;

namespace TestServer
{
    public interface IMath
    {
        int Sum(int a, int b);
        int Mul(int a, int b);
        int Constant { get; set; }
    }
    public class Math : IMath
    {
        private int _constant;
        public int Constant {
            get 
            {
                return _constant;
            }
            set
            {
                _constant = value;
                Console.WriteLine(_constant);
            }
        }
        public int Sum(int a, int b) => a + b;
        public int Mul(int a, int b) => a * b;
    }
    internal class Program
    {
        static void Main()
        {
            var server = new RPCServer();
            server.AddImplementation<IMath>(new Math());
            while (true) ;
        }
    }
}