﻿using RemoteProcedureCalls.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using TestCore;

namespace TestServer
{
    public class Test : ITest
    {
        public Test(int index) 
        {
            Value = index;
        }
        public event Action<string> OnCall;
        public void Call()
        {
            OnCall?.Invoke(Value.ToString());
        }
        public int Value { get; set; }
        public int Sum(int a, int b) => a + b;
        public int Sum(int a, int b, int c) => a + b + c;
        public int Mul(int a, int b) => a * b;

        public void Act(Action<string> action)
        {
            action("WoW, Text!");
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
            server.OnClientConnected += Server_OnClientConnected;
            server.OnClientClosed += Server_OnClientClosed;
            int index = 0;
            List<Test> tests = new List<Test>();
            server.AddImplementation<ITest>(() => 
            {
                Test test = new Test(index++);
                tests.Add(test);
                return test;
            });
            var counter = new MyCounter();
            server.AddImplementation<ICounter>(() => counter);
            while (true)
            {
                Thread.Sleep(1000);
                foreach(Test test in tests)
                {
                    test.Call();
                }
            }
        }

        private static void Server_OnClientConnected(string obj)
        {
            Console.WriteLine($"Client connected");
        }

        private static void Server_OnClientClosed(string obj)
        {
            Console.WriteLine($"Client closed");
        }
    }
}