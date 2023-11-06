# Библиотека RPC (Remote Procedure Call) на C#

Эта библиотека предоставляет механизм удаленных вызовов процедур для вашего C# проекта. Она позволяет вам создавать удаленные интерфейсы и реализации для взаимодействия между клиентом и сервером.

## Пример использования

### Пример описания интерфейсов

```csharp
public interface ICounter
{
    void Clear();
    int Counter { get; }
}

public interface ITest
{
    int Sum(int a, int b);
    int Sum(int a, int b, int c);
    int Mul(int a, int b);
    int Value { get; set; }
    void Act(Action<string> action);
    event Action<string> OnCall;
}
```

### Пример реализации на сервере

```csharp
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
```
### Пример клиента

```csharp
static void Main(string[] args)
{
    var client = new RPCClient();
    try
    {
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
        for(int i = 0; i < 100000; i++)
        {
            Console.WriteLine(counter.Counter);
        }
    }
    catch (ExtendedSocketClosedException)
    {
        Console.WriteLine($"Server closed");
    }
    Console.WriteLine($"Press any button...");
    Console.ReadKey();
}

private static void Test_OnCall(string text)
{
    Console.WriteLine("Tururu {0}", text);
}
```

## Зависимости

Эта библиотека не имеет зависимостей и может быть легко интегрирована в ваш проект на C#.

## Лицензия

Эта библиотека распространяется под лицензией MIT.

## Связь

Если у вас возникли вопросы или проблемы с использованием этой библиотеки, не стесняйтесь связаться с нами по адресу gigster2000@yandex.ru.
