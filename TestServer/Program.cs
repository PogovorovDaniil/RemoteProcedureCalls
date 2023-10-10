using RemoteProcedureCalls.Core;
using System;
using TestCore;

namespace TestServer
{
    public class Test : ITest
    {
        private int _value;
        public int Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                Console.WriteLine(_value);
            }
        }
        public int Sum(int a, int b) => a + b;
        public int Sum(int a, int b, int c) => a + b + c;
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

    public class MyCounter : ICounter
    {
        int counter = 0;
        public int Counter => counter++;

        public void Clear()
        {
            counter = 0;
        }
    }
    internal class Program
    {
        static void Main()
        {
            var server = new RPCServer();
            server.AddImplementation<ITest>(() => new Test());
            var counter = new MyCounter();
            server.AddImplementation<ICounter>(() => counter);
            while (true) ;
        }
    }
}