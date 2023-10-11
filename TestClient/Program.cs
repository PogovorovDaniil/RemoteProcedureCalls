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
            test.OnCall += Test_OnCall;

            var counter = client.GetImplementation<ICounter>();
            counter.Clear();
            for(int i = 0; i < 10; i++)
            {
                Console.WriteLine(counter.Counter);
            }
        }

        private static void Test_OnCall(string text)
        {
            Console.WriteLine("Tururu {0}", text);
        }
    }
}