using RemoteProcedureCalls;
using System;

namespace TestClient
{
    public interface IMath
    {
        int Sum(int a, int b);
        int Mul(int a, int b);
    }
    internal class Program
    {
        static void Main(string[] args)
        {
            string address = Console.ReadLine();
            var client = new RPCClient(address);
            var imp = client.GetImplementation<IMath>();

            Console.WriteLine(imp.Sum(40, 2));
            Console.WriteLine(imp.Mul(2, 2));
            while (true) ;
        }
    }
}