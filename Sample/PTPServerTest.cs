using System;
using System.Net;
using System.Threading;
using Eyu.Audio.Aes67;
using Eyu.Audio.PTP;

namespace Sample
{
    public class PTPServerTest
    {
        public static void Test(string[] args)
        {
            Console.WriteLine("PTP Clock Test");
            Console.WriteLine("=====================");
            Console.WriteLine("1. Slave Test        (priority1=200, sync to existing master)");
            Console.WriteLine("2. Master Test       (priority1=1, become master)");
            Console.WriteLine("3. Role Switch       (interactive priority change)");
            Console.WriteLine("4. AES67 Broadcast   (choose master/slave + file broadcast)");
            Console.Write("\nSelect test [1-4]: ");

            var key = Console.ReadLine()?.Trim();
            switch (key)
            {
                case "1": TestSlave(); break;
                case "2": TestMaster(); break;
                case "3": TestRoleSwitch(); break;
                case "4": TestAes67Broadcast(); break;
                default: TestSlave(); break;
            }
        }

        static void TestSlave()
        {
            Console.WriteLine("\n=== Slave Clock Test ===");
            var clock = PTPClock.Instance;
            clock.OnRoleChanged += isMaster =>
                Console.WriteLine($"  [Role Changed] => {(isMaster ? "MASTER" : "SLAVE")}");

            clock.Start(priority1: 200, priority2: 200);
            Console.WriteLine($"Clock ID : {BitConverter.ToString(clock.ClockId)}");
            Console.WriteLine($"Priority : {clock.Priority1} / {clock.Priority2}");
            Console.WriteLine("Waiting for BMCA election and sync...\n");

            PrintStatusLoop(clock);
        }

        static void TestMaster()
        {
            Console.WriteLine("\n=== Master Clock Test ===");
            var clock = PTPClock.Instance;
            clock.OnRoleChanged += isMaster =>
                Console.WriteLine($"  [Role Changed] => {(isMaster ? "MASTER" : "SLAVE")}");

            clock.Start(priority1: 1, priority2: 1);
            Console.WriteLine($"Clock ID : {BitConverter.ToString(clock.ClockId)}");
            Console.WriteLine($"Priority : {clock.Priority1} / {clock.Priority2}");

            Console.WriteLine("Waiting for BMCA election...");
            for (int i = 0; i < 50 && !clock.IsMaster; i++)
                Thread.Sleep(100);

            Console.WriteLine($"Role     : {(clock.IsMaster ? "MASTER" : "SLAVE")}");
            Console.WriteLine($"IsSynced : {clock.IsSynced}\n");

            PrintStatusLoop(clock);
        }

        static void TestRoleSwitch()
        {
            Console.WriteLine("\n=== Role Switch Test ===");
            var clock = PTPClock.Instance;
            clock.OnRoleChanged += isMaster =>
                Console.WriteLine($"\n  >>> [Role Changed] => {(isMaster ? "MASTER" : "SLAVE")} <<<\n");

            Console.WriteLine("Starting with priority1=200 (expect slave if another clock exists)...");
            clock.Start(priority1: 200, priority2: 200);
            Console.WriteLine($"Clock ID : {BitConverter.ToString(clock.ClockId)}");

            var running = true;
            var statusThread = new Thread(() =>
            {
                while (running)
                {
                    PrintStatus(clock);
                    Thread.Sleep(2000);
                }
            });
            statusThread.IsBackground = true;
            statusThread.Start();

            Console.WriteLine("\nCommands:");
            Console.WriteLine("  m  = set priority1=1   (request master role)");
            Console.WriteLine("  s  = set priority1=255 (request slave role)");
            Console.WriteLine("  q  = quit\n");

            while (true)
            {
                var input = Console.ReadLine()?.Trim().ToLower();
                if (input == "q") break;
                if (input == "m")
                {
                    Console.WriteLine("  -> Setting priority1=1 (requesting master)...");
                    clock.Priority1 = 1;
                    clock.Priority2 = 1;
                }
                else if (input == "s")
                {
                    Console.WriteLine("  -> Setting priority1=255 (requesting slave)...");
                    clock.Priority1 = 255;
                    clock.Priority2 = 255;
                }
            }

            running = false;
            clock.Stop();
            Console.WriteLine("Test completed.");
        }

        static void TestAes67Broadcast()
        {
            Console.WriteLine("\n=== AES67 File Broadcast Test ===");
            Console.WriteLine("Choose PTP clock role:");
            Console.WriteLine("  1. Master (priority1=1)");
            Console.WriteLine("  2. Slave  (priority1=200)");
            Console.Write("Select [1-2]: ");
            var roleInput = Console.ReadLine()?.Trim();
            bool asMaster = roleInput != "2";

            var clock = PTPClock.Instance;
            clock.OnRoleChanged += isMaster =>
                Console.WriteLine($"  [Role Changed] => {(isMaster ? "MASTER" : "SLAVE")}");

            if (asMaster)
            {
                clock.Start(priority1: 1, priority2: 1);
                Console.WriteLine("Starting as Master (priority1=1)...");
                Console.WriteLine("Waiting for BMCA election...");
                for (int i = 0; i < 50 && !clock.IsMaster; i++)
                    Thread.Sleep(100);
            }
            else
            {
                clock.Start(priority1: 200, priority2: 200);
                Console.WriteLine("Starting as Slave (priority1=200)...");
                Console.WriteLine("Waiting for sync...");
                for (int i = 0; i < 50 && !clock.IsSynced; i++)
                    Thread.Sleep(200);
            }

            Console.WriteLine($"Clock ID : {BitConverter.ToString(clock.ClockId)}");
            Console.WriteLine($"Role     : {(clock.IsMaster ? "MASTER" : "SLAVE")}");
            Console.WriteLine($"IsSynced : {clock.IsSynced}");

            if (!clock.IsSynced)
            {
                Console.WriteLine("Warning: PTP not synced, timestamps may be inaccurate.");
            }

            Console.Write("\nEnter audio file path: ");
            var path = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("No file path provided. Exiting.");
                clock.Stop();
                return;
            }

            var localIp = Aes67FileBroadcastTest.GetFirstLocalIPv4();
            if (localIp == null)
            {
                Console.WriteLine("No local IPv4 address found. Exiting.");
                clock.Stop();
                return;
            }

            Console.WriteLine($"Local IP : {localIp}");
            Aes67FileBroadcastTest.BroadcastFromFile(path, "AES67 Broadcast", localIp);

            Console.WriteLine("\nPress any key to stop...");
            Console.ReadKey();
            clock.Stop();
            Console.WriteLine("Test completed.");
        }

        static void PrintStatusLoop(PTPClock clock)
        {
            Console.WriteLine("Press 'q' to stop.\n");
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q') break;
                }
                PrintStatus(clock);
                Thread.Sleep(2000);
            }
            clock.Stop();
            Console.WriteLine("Test completed.");
        }

        static void PrintStatus(PTPClock clock)
        {
            var role = clock.IsMaster ? "Master" : "Slave ";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Role={role}  Synced={clock.IsSynced,-5}  " +
                $"Master={clock.PtpMaster,-30}  Offset={clock.Offset}  Delay={clock.Delay}");
        }
    }
}
