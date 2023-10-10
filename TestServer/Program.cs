using RemoteProcedureCalls.Core;
using System;
using TestCore;

namespace TestServer
{
    public class Test : ITest
    {
        private int _constant;
        public int Constant
        {
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

        public void Act(Action<string> action)
        {
            Console.WriteLine("Start");
            action("WoW, Text!");
            Console.WriteLine("End");
        }

        public Func<int, int, int> GetSum()
        {
            Console.WriteLine("GetSum");
            return (a, b) => a + b;
        }
    }
    internal class Program
    {
        static void Main()
        {
            var server = new RPCServer();
            server.AddImplementation<ITest>(new Test());
            while (true) ;
        }
    }
}