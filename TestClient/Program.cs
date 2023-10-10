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
            var imp = client.GetImplementation<ITest>();

            Console.WriteLine(imp.Sum(40, 2));
            Console.WriteLine(imp.Sum(1, 2, 3));
            Console.WriteLine(imp.Mul(2, 2));
            imp.Constant = 42;

            imp.Act((text) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(text);
                Console.ResetColor();
            });
            Console.WriteLine(imp.GetSum()(30, 6));
            while (true) ;
        }
    }
}