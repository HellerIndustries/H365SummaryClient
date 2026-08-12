using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace H365SummaryClient
{
    class Program
    {
        private const int QUERY_RESPONSE_PORT = 31010;
        private const int NOTIFICATION_PORT = 31011;
        private const int GUID_BYTES = 16;
        private const int INT_BYTES = 4;
        private const int FIXED_SIZE_BUFFER = 64 * 1024;

        static void Main(string[] args)
        {
            string ip = "127.0.0.1";

            if (args.Length >= 1)
                ip = args[0];

            Console.Write("Please enter the Query ID: ");
            int queryId = int.Parse(Console.ReadLine());

            Console.Write("Please enter the query payload (or press Enter for none): ");
            string query = Console.ReadLine() ?? string.Empty;

            try
            {
                using (Socket notificationSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                using (Socket querySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    IPAddress address = IPAddress.Parse(ip);

                    Console.WriteLine($"Connecting to notification port {NOTIFICATION_PORT}...");
                    notificationSocket.Connect(new IPEndPoint(address, NOTIFICATION_PORT));

                    Console.WriteLine($"Connecting to query/response port {QUERY_RESPONSE_PORT}...");
                    querySocket.Connect(new IPEndPoint(address, QUERY_RESPONSE_PORT));

                    byte[] queryPacket = CreatePacket(Guid.NewGuid(), queryId, query);
                    SendBuffer(querySocket, queryPacket);

                    Console.WriteLine($"Query {queryId} sent. Waiting for its response...\n");

                    byte[] response = new byte[FIXED_SIZE_BUFFER];
                    if (!ReceiveBuffer(querySocket, response))
                    {
                        Console.WriteLine("The query/response connection was closed by the server.");
                        return;
                    }

                    ProcessPacket(response, response.Length);
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
        }

        static byte[] CreatePacket(Guid guid, int queryId, string query)
        {
            byte[] messageBytes = Encoding.ASCII.GetBytes(query);
            if (messageBytes.Length > FIXED_SIZE_BUFFER - GUID_BYTES - INT_BYTES)
                throw new ArgumentException("The query payload is too large.", nameof(query));

            byte[] packet = new byte[FIXED_SIZE_BUFFER];
            Array.Copy(guid.ToByteArray(), 0, packet, 0, GUID_BYTES);
            Array.Copy(BitConverter.GetBytes(queryId), 0, packet, GUID_BYTES, INT_BYTES);
            Array.Copy(messageBytes, 0, packet, GUID_BYTES + INT_BYTES, messageBytes.Length);

            return packet;
        }

        static void SendBuffer(Socket socket, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
                offset += socket.Send(buffer, offset, buffer.Length - offset, SocketFlags.None);
        }

        static bool ReceiveBuffer(Socket socket, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int bytesReceived = socket.Receive(buffer, offset, buffer.Length - offset, SocketFlags.None);
                if (bytesReceived == 0)
                    return false;

                offset += bytesReceived;
            }

            return true;
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

            if (functionId == 392)
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
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] === Query Response Received ===");
                Console.WriteLine($"  GUID: {packetGuid}");
                Console.WriteLine($"  Function ID: {functionId}");
                Console.WriteLine($"  Message: {message}");
                Console.WriteLine();
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
