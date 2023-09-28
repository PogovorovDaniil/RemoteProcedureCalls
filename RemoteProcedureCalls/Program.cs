using System;

namespace RemoteProcedureCalls
{
    public interface ICow
    {
        void Say(string text);
    }

    public class Program
    {
        public static object Method(string interfaceName, string methodName, object[] parameters, Type returnType)
        {
            Console.WriteLine($"{returnType} {interfaceName}.{methodName}({string.Join(", ", parameters)});");
            return returnType.IsValueType ? 0 : null;
        }
        public static void Main()
        {
            var factory = new ImplementationFactory(Method);
            var test = factory.Create<ICow>();
            test.Say("Moooo");
        }
    }
}