using RemoteProcedureCalls.Core;
using System;
using TestCore;

namespace TestClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var client = new RPCClient();
            var test = client.GetImplementation<ITest>();

            Console.WriteLine(test.Sum(40, 2));
            Console.WriteLine(test.Sum(1, 2, 3));
            Console.WriteLine(test.Mul(2, 2));
            test.Value = 42;

            test.Act((text) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(text);
                Console.ResetColor();
            });
            Console.WriteLine(test.GetSum()(30, 6));

            var counter = client.GetImplementation<ICounter>();
            counter.Clear();
            while (true)
            {
                Console.WriteLine(counter.Counter);
            }
        }
    }
}