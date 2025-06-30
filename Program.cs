using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

class Program
{
    class Packet
    {
        public string Symbol = "";
        public char BuySell;
        public int Quantity;
        public int Price;
        public int Sequence;

        public override string ToString()
        {
            return $"Seq: {Sequence} | {Symbol} | {BuySell} | Qty: {Quantity} | Price: {Price}";
        }
    }

    static void Main()
    {
        string host = "127.0.0.1"; // Localhost
        int port = 3000;

        try
        {
            List<Packet> packets = new List<Packet>();
            using (TcpClient client = new TcpClient(host, port))
            using (NetworkStream stream = client.GetStream())
            {
                // Step 1: Send request to stream all packets
                byte[] request = new byte[] { 1, 0 };
                stream.Write(request, 0, request.Length);

                // Step 2: Read until server closes connection
                byte[] buffer = new byte[17];
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    if (bytesRead < 17) continue;

                    Packet pkt = ParsePacket(buffer);
                    packets.Add(pkt);
                    Console.WriteLine(pkt);
                }
            }

            // Step 3: Find missing sequences
            HashSet<int> receivedSeqs = new HashSet<int>();
            foreach (var p in packets) receivedSeqs.Add(p.Sequence);

            int maxSeq = 0;
            foreach (var p in packets) if (p.Sequence > maxSeq) maxSeq = p.Sequence;

            List<int> missing = new List<int>();
            for (int i = 1; i <= maxSeq; i++)
                if (!receivedSeqs.Contains(i))
                    missing.Add(i);

            Console.WriteLine("\nMissing Packets: " + string.Join(",", missing));

            // Step 4: Request missing packets
            foreach (int seq in missing)
            {
                using (TcpClient resendClient = new TcpClient(host, port))
                using (NetworkStream stream = resendClient.GetStream())
                {
                    byte[] resend = new byte[] { 2, (byte)seq };
                    stream.Write(resend, 0, resend.Length);

                    byte[] buffer = new byte[17];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 17)
                    {
                        Packet pkt = ParsePacket(buffer);
                        Console.WriteLine("Resent: " + pkt);
                        packets.Add(pkt);
                    }
                }
            }

            Console.WriteLine("\n✅ Final Packet Count: " + packets.Count);
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Error: " + ex.Message);
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    static Packet ParsePacket(byte[] buffer)
    {
        Packet p = new Packet
        {
            Symbol = Encoding.ASCII.GetString(buffer, 0, 4),
            BuySell = (char)buffer[4],
            Quantity = ReadInt32BE(buffer, 5),
            Price = ReadInt32BE(buffer, 9),
            Sequence = ReadInt32BE(buffer, 13)
        };
        return p;
    }

    static int ReadInt32BE(byte[] data, int offset)
    {
        return (data[offset] << 24) |
               (data[offset + 1] << 16) |
               (data[offset + 2] << 8) |
               (data[offset + 3]);
    }
}
