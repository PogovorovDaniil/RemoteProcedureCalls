namespace TestCore
{
    public interface ITest
    {
        int Sum(int a, int b);
        int Sum(int a, int b, int c);
        int Mul(int a, int b);
        int Value { get; set; }
        void Act(Action<string> action);
        event Action<string> OnCall;
    }
}