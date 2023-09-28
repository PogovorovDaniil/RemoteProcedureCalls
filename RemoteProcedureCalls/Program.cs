using System;

namespace RemoteProcedureCalls
{
    public interface IMath
    {
        int Sum(int a, int b);
        int Mul(int a, int b);
    }

    public class Program
    {
        public static object Method(string interfaceName, string methodName, object[] parameters, Type returnType)
        {
            if(interfaceName == "IMath")
            {
                if(methodName == "Sum")
                {
                    return (int)parameters[0] + (int)parameters[1];
                }
                if(methodName == "Mul")
                {
                    return (int)parameters[0] * (int)parameters[1];
                }
            }
            return null;
        }

        public static void Main()
        {
            var factory = new ImplementationFactory(Method);
            IMath math = factory.Create<IMath>();
            Console.WriteLine(math.Sum(4, 5));
            Console.WriteLine(math.Mul(4, 5));
        }
    }
}