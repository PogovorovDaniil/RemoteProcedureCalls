using RemoteProcedureCalls;
using System;

namespace TestClient
{
    public interface IMath
    {
        int Sum(int a, int b);
        int Mul(int a, int b);
        void Print(string text);
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
            char[] chars = new char[2048];
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = 'ы';
            }
            imp.Print(new string(chars));
            while (true) ;
        }
    }
}