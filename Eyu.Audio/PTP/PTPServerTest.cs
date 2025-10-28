using System;
using System.Threading;
using Eyu.Audio.PTP;

namespace Eyu.Audio.PTP.Test
{
    public class PTPServerTest
    {
        public static void Test(string[] args)
        {
            Console.WriteLine("PTP Master Clock Server Test");
            Console.WriteLine("This server will act as a PTP master clock, implementing the following functionality:");
            Console.WriteLine("1. Send SYNC packets at regular intervals");
            Console.WriteLine("2. After sending SYNC, send FOLLOW_UP packet containing the exact time when SYNC was sent");
            Console.WriteLine("3. Receive DELAY_REQ packets from clients");
            Console.WriteLine("4. Send DELAY_RESP packets containing the exact time when DELAY_REQ was received");
            Console.WriteLine();
            
            // 创建PTP服务器实例
            var server = PTPClock.Instance;

            // 初始化服务器 as master with high priority
            server.Initialize(priority1:130,priority2:130); // Set high priority to ensure master role
            
            Console.WriteLine($"Server ID: {server.ClockId}");
            Console.WriteLine($"Domain: {server.Domain}");
            Console.WriteLine($"Priority1: {server.Priority1}");
            Console.WriteLine($"Priority2: {server.Priority2}");
            Console.WriteLine($"Is Master: {server.IsMaster}");
            Console.WriteLine($"Is Running: {server.IsRunning}");
            Console.WriteLine();
            
            // 启动服务器
            server.Start();
            
            Console.WriteLine($"Server started. Is Running: {server.IsRunning}");
            Console.WriteLine($"Current role: {(server.IsMaster ? "Master" : "Slave")}");
            Console.WriteLine();
            Console.WriteLine("PTP Master Clock is now running...");
            Console.WriteLine("- SYNC and FOLLOW_UP messages are sent every second");
            Console.WriteLine("- ANNOUNCE messages are sent every second");
            Console.WriteLine("- Listening for DELAY_REQ messages and responding with DELAY_RESP");
            Console.WriteLine();
            Console.WriteLine("Press any key to stop the server...");
            
            Console.ReadKey();
            
            // 停止服务器
            server.Stop();
            
            Console.WriteLine();
            Console.WriteLine("Server stopped.");
            Console.WriteLine("Test completed.");
        }
    }
}
