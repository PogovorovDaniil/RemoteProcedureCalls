﻿namespace TestCore
{
    public interface ITest
    {
        int Sum(int a, int b);
        int Mul(int a, int b);
        int Constant { get; set; }
        void Act(Action<string> action);
        Func<int, int, int> GetSum();
    }
}