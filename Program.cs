using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace H365SummaryClient
{
    class Program
    {
        private const int NOTIFICATION_H365_SUMMARY_AVAILABLE = 392;
        private const int GUID_BYTES = 16;
        private const int INT_BYTES = 4;
        private const int BUFFER_SIZE = 4096;

        static void Main(string[] args)
        {
            string ip = "127.0.0.1";
            int port = 31011;

            // Parse command line arguments
            if (args.Length >= 1)
                ip = args[0];
            if (args.Length >= 2)
                int.TryParse(args[1], out port);

            Console.WriteLine($"H365 Summary Client - Listening for NOTIFICATION_H365_SUMMARY_AVAILABLE (ID: {NOTIFICATION_H365_SUMMARY_AVAILABLE})");
            Console.WriteLine($"Connecting to {ip}:{port}...");
            Console.WriteLine("Press Ctrl+C to exit.\n");

            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                    Console.WriteLine("Connected! Waiting for notifications...\n");

                    byte[] buffer = new byte[BUFFER_SIZE];

                    while (true)
                    {
                        int bytesReceived = socket.Receive(buffer);
                        if (bytesReceived == 0)
                        {
                            Console.WriteLine("Connection closed by server.");
                            break;
                        }

                        ProcessPacket(buffer, bytesReceived);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void ProcessPacket(byte[] buffer, int length)
        {
            if (length < GUID_BYTES + INT_BYTES)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Received incomplete packet ({length} bytes)");
                return;
            }

            // Extract GUID
            byte[] guidBytes = new byte[GUID_BYTES];
            Array.Copy(buffer, 0, guidBytes, 0, GUID_BYTES);
            Guid packetGuid = new Guid(guidBytes);

            // Extract function ID
            byte[] funcBytes = new byte[INT_BYTES];
            Array.Copy(buffer, GUID_BYTES, funcBytes, 0, INT_BYTES);
            int functionId = BitConverter.ToInt32(funcBytes, 0);

            // Extract message
            int messageLength = length - GUID_BYTES - INT_BYTES;
            string message = string.Empty;
            if (messageLength > 0)
            {
                byte[] messageBytes = new byte[messageLength];
                Array.Copy(buffer, GUID_BYTES + INT_BYTES, messageBytes, 0, messageLength);
                message = Encoding.ASCII.GetString(messageBytes).TrimEnd('\0');
            }

            // Check if this is the notification we're looking for
            if (functionId == NOTIFICATION_H365_SUMMARY_AVAILABLE)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] === NOTIFICATION_H365_SUMMARY_AVAILABLE Received ===");
                Console.WriteLine($"  GUID: {packetGuid}");
                Console.WriteLine($"  Function ID: {functionId}");

                try
                {
                    var summary = JsonConvert.DeserializeObject<H365Summary>(message);
                    Console.WriteLine($"  SubstrateID: {summary.SubstrateID}");
                    Console.WriteLine($"  PeakTemperature: [{FormatArray(summary.PeakTemperature)}]");
                    Console.WriteLine($"  HeatUpTime: [{FormatArray(summary.HeatUpTime)}]");
                    Console.WriteLine($"  SoakTime: [{FormatArray(summary.SoakTime)}]");
                    Console.WriteLine($"  TimeAboveLiquidous: [{FormatArray(summary.TimeAboveLiquidous)}]");
                    Console.WriteLine($"  CoolingRate: [{FormatArray(summary.CoolingRate)}]");
                    Console.WriteLine($"  OvenPWI: {summary.OvenPWI}");
                    Console.WriteLine($"  VPFilePath: {summary.VPFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Raw Message: {message}");
                    Console.WriteLine($"  (Parse error: {ex.Message})");
                }
                Console.WriteLine();
            }
            else
            {
                
            }
        }

        static string FormatArray<T>(T[] arr)
        {
            if (arr == null) return "null";
            return string.Join(", ", arr);
        }
    }

    public class H365Summary
    {
        public int LaneID { get; set; }
        public string SubstrateID { get; set; }
        public float[] PeakTemperature { get; set; }
        public int[] HeatUpTime { get; set; }
        public int[] SoakTime { get; set; }
        public int[] TimeAboveLiquidous { get; set; }
        public double[] CoolingRate { get; set; }
        public double OvenPWI { get; set; }
        public string VPFilePath { get; set; }
    }
}
