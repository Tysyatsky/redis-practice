
using StackExchange.Redis;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Threading.Timer;

namespace redis_lab5
{
    class Program
    {

        static IDatabase _db;
        static ISubscriber _subscriber;
        static string _listName;

        static private Timer _timer = null;

        static readonly ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(
            new ConfigurationOptions
            {
                EndPoints = { "localhost:6379" }
            });

        static async Task Main(string[] args)
        {
            _db = redis.GetDatabase();
            _subscriber = redis.GetSubscriber();
            await Menu(_db, _subscriber);
        }
        static async Task Menu(IDatabase db, ISubscriber sub)
        {
            try
            {
                Console.WriteLine("Menu");
                Console.WriteLine("1. Create/Incr counter");
                Console.WriteLine("2. Push object in list");
                Console.WriteLine("3. Pop object from list");
                Console.WriteLine("4. Save list file");
                Console.WriteLine("5. Subscribe");
                Console.WriteLine("0. Exit");

                switch (Console.ReadKey().Key)
                {
                    case ConsoleKey.D1:
                        Console.WriteLine("Create/Incr counter");
                        Console.Write("Enter key: ");
                        var key = Console.ReadLine() ?? "default-counter";
                        CreateCounter(db, key, 0).Wait();
                        Console.WriteLine();
                        var count = await GetCounterIncr(db, key);
                        Console.WriteLine($"Key: {key} New Count: {count}");
                        await Menu(db, sub);
                        break;
                    case ConsoleKey.D2:
                        Console.WriteLine("Push object in list");
                        await QueuePush(db, sub);
                        await Menu(db, sub);
                        break;
                    case ConsoleKey.D3:
                        Console.WriteLine("Pop object from list");
                        await QueuePop(db, sub);
                        await Menu(db, sub);
                        break;
                    case ConsoleKey.D4:
                        Console.WriteLine("Save list file");
                        Console.Write("Enter key: ");
                        var keyToSave = Console.ReadLine() ?? "default-key";
                        await SaveFile(db, sub, keyToSave);
                        await Menu(db, sub);
                        break;
                    case ConsoleKey.D5:
                        await Sub();
                        await Menu(db, sub);
                        break;
                    case ConsoleKey.D0:
                        break;
                    default:
                        Console.Clear();
                        var pong = await db.PingAsync();
                        Console.WriteLine(pong);
                        break;
                }
            }
            catch (NullReferenceException ex)
            {
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        static async Task Sub()
        {
            _subscriber = redis.GetSubscriber();
            Console.WriteLine("Enter blocked queue name: ");
            _listName = Console.ReadLine();
            try
            {
                _subscriber.Subscribe("Queue", delegate
                {
                    for (int i = 0; i < _db.ListLength(_listName); i++)
                    {
                        Console.WriteLine(_db.ListGetByIndex(_listName, i));
                    }
                });
            }
            catch (Exception)
            {
                Console.WriteLine("List was not found");
            }
            _timer = new Timer(timer_Tick, null, 0, 2000);
            Thread.Sleep(Timeout.Infinite);
        }

        static void timer_Tick(object sender)
        {
            Task.Run(DisplayBlockedQueue);
        }

        static async Task DisplayBlockedQueue()
        {
            //_db = redis.GetDatabase();
            //Console.WriteLine("Press esc to quit");
            _subscriber.Publish("Queue", "");
            
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
            {
                Console.Clear();
                _timer.Dispose();
                Environment.Exit(0);
            }
            Console.Clear();
        }
        static async Task SaveFile(IDatabase? db, ISubscriber sub, string key)
        {
            List<string> values = new List<string>();
            if (db == null || !db.KeyExists(key))
                throw new NullReferenceException();
            for (int i = 0; i < db.ListLength(key); i++)
            {
                string work = db.ListRightPop(key);
                if (work != null) Console.WriteLine($"Popped value: {work}");
                values.Add(work);
                db.ListLeftPush(key, work);
            }
            File.AppendAllLines(key + ".txt", values);

        }
        static async Task QueuePop(IDatabase? db, ISubscriber sub)
        {
            Console.Write("Enter key: ");
            var keyToPop = Console.ReadLine() ?? "default-key";

            string work = db.ListRightPop(keyToPop);
            if (work != null) Console.WriteLine($"Popped value: {work}");
        }

        static async Task QueuePush(IDatabase? db, ISubscriber sub)
        {
            Console.Write("Enter key: ");
            var keyToInsert = Console.ReadLine() ?? "default-key";
            Console.Write("Enter value: ");
            var valueToInsert = Console.ReadLine() ?? "default-value";
            db.ListLeftPush(keyToInsert, valueToInsert);
        }

        static async Task CreateCounter(IDatabase? db, string key, int value)
        {
            var tran = db.CreateTransaction();
            tran.StringSetAsync(key, value);
            tran.StringIncrementAsync(key);
            bool committed = tran.Execute();
        }

        static async Task<RedisValue> GetCounterIncr(IDatabase? db, string key)
        {
            if (db != null)
                return db.StringGet(key);

            throw new NullReferenceException();
        }
    }
}