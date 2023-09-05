using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Configuration;
using betacrew_aditya.UTIL;
using betacrew_aditya.Packet;
using betacrew_aditya.EventCollection;
namespace BetaCrewClientApp
{
    class BetaCrew
    {
        static void Main(string[] args)
        {
            EventCollection.Clear();
            EventCollection.Log("Start Application");

            try
            {
                ConnectTcpClient(out TcpClient client);

                Process(client);

                client.Close();
            }
            catch (SocketException e)
            {
                EventCollection.Log($"SocketException: {e}" + e);
            }
            catch (Exception ex)
            {
                EventCollection.Log($"Error: {ex.Message}");
            }
        }

        // establishing connection
        public static void ConnectTcpClient(out TcpClient client)
        {
            string ipAddress = 
            ConfigurationManager.AppSettings.Get("IPADDRESS") ?? 
            Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? 
            throw new Exception("No network adapters with an IPv4 address in the system!");

            EventCollection.Log("IP Address Fetched Successfully");
                
            int port = int.TryParse(ConfigurationManager.AppSettings.Get("PORT"), out int parsedPort) ? parsedPort : 3000;
            client = new TcpClient();
            client.Connect(ipAddress, port);

            EventCollection.Log("TCP Client Connection: " + (client.Connected ? "Succeeded" : "Failed"));     
        }      

        // process response payload
        static void Process(TcpClient client) 
        {
            using NetworkStream stream = client.GetStream();

            // send a request to stream all packets
            byte[] requestPayload = new byte[2];
            requestPayload[0] = 1; // Call Type 1 for "Stream All Packets"
            requestPayload[1] = 0;

            stream.Write(requestPayload, 0, requestPayload.Length);
            EventCollection.Log("Request \"Stream All Packets\" Sent");

            // create a list to store received packets
            List<Packet> receivedPackets = new List<Packet>();

            while (true)
            {
                byte[] buffer = new byte[UTIL.PACKETSIZE];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                    break; // Server closed the connection

                // parse the received packet
                Packet packet = ParsePacket(buffer);

                // add the packet to the list
                receivedPackets.Add(packet);
            }

            EventCollection.Log("Received Packets Count = " + receivedPackets.Count);

            // handle missing sequences
            List<int> missingSequences = FindMissingSequences(receivedPackets);
            EventCollection.Log("Missing Packets Count = " + missingSequences.Count);

            ConnectTcpClient(out client);
            using (NetworkStream stream1 = client.GetStream())
            {
                foreach (int missingSeq in missingSequences)
                {
                    // Request missing packets one by one
                    byte[] resendRequest = CreateResendRequest(missingSeq);
                    stream1.Write(resendRequest, 0, resendRequest.Length);

                    // Receive and process the missing packet
                    byte[] buffer = new byte[UTIL.PACKETSIZE];
                    int bytesRead = stream1.Read(buffer, 0, buffer.Length);
                    Console.WriteLine("BytesRead Missing = " + bytesRead);
                    Console.WriteLine("DataPoints Missing = " + bytesRead / 17);
                    if (bytesRead == 0)
                        break; // server closed the connection

                    // parse and process the missing packet
                    Packet packet = ParsePacket(buffer);

                    // add the packet to the list
                    receivedPackets.Add(packet);
                }
            }
            client.Close();
            
            Console.WriteLine("Completed");


            // generate a JSON file with the collected data
            GenerateJsonOutput(receivedPackets);
            Console.WriteLine("JSON created");
        }
        
        // parse a response packet and create a Packet object
        public static Packet ParsePacket(byte[] buffer)
        {
            Console.WriteLine("byte array: " + BitConverter.ToString(buffer));

            Packet packet = new Packet();
            packet.Symbol = Encoding.ASCII.GetString(buffer, 0, 4);
            packet.BuySellIndicator = (char)buffer[4];
            packet.Quantity = HexadecimalToInt32(buffer, 5);
            packet.Price = HexadecimalToInt32(buffer, 9);
            packet.Sequence = HexadecimalToInt32(buffer, 13);
            return packet;
        }

        public static int HexadecimalToInt32(byte[] buffer, int startIdx)
        {
            byte[] subArray = new byte[UTIL.INT32SIZE];
            Array.Copy(buffer, startIdx, subArray, 0, UTIL.INT32SIZE);
            Array.Reverse(subArray);

            return BitConverter.ToInt32(subArray, 0);
        }

        // find missing sequences in the received packets
        static List<int> FindMissingSequences(List<Packet> receivedPackets)
        {
            HashSet<int> set = new();
            List<int> missingSequences = new();
            int lastSeq = receivedPackets.Max(p => p.Sequence); // last sequence is never missed

            foreach (Packet packet in receivedPackets)
            {
                set.Add(packet.Sequence);
            }

            for (int i = 1; i < lastSeq; i++)
            {
                if(!set.Contains(i)) missingSequences.Add(i);
            }

            return missingSequences;
        }

        // create a binary request to resend a specific packet
        static byte[] CreateResendRequest(int sequence)
        {
            byte[] requestPayload = new byte[2];
            requestPayload[0] = 2; // Call Type 2 for "Resend Packet"
            requestPayload[1] = Convert.ToByte(sequence);
            return requestPayload;
        }

        // generate a JSON file from the received packets
        static void GenerateJsonOutput(List<Packet> packets)
        {
            // sorting accoridng to Sequence
            List<Packet> sortedList = packets.OrderBy(p => p.Sequence).ToList();

            // create an array of JSON objects from the Packet objects
            var jsonObjects = sortedList.Select(p => new
            {
                Symbol = p.Symbol,
                BuySellIndicator = p.BuySellIndicator,
                Quantity = p.Quantity,
                Price = p.Price,
                Sequence = p.Sequence
            }).ToArray();

            // serialize the array to JSON and write it to a file
            string json = JsonConvert.SerializeObject(jsonObjects, Formatting.Indented);
            File.WriteAllText(ConfigurationManager.AppSettings.Get("FILEOUTPUTPATH")?? "outdump.json", json);
        }
    }
}
