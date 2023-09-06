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
            EventCollection.Log("Client application started.");

            try
            {
                Process();
            }
            catch (ConfigurationErrorsException)
            {
                EventCollection.Log("Error: Unable to read the configuration file (App.config). Please ensure it's correctly configured.");
            }
            catch (SocketException e)
            {
                EventCollection.Log($"Error: {e.Message}");
            }
            catch (Exception ex)
            {
                EventCollection.Log($"Error: {ex.Message}");
            }
        }

        private static int ServerPort
        {
            get { return Int32.TryParse(ConfigurationManager.AppSettings.Get("PORT"), out int parsedPort) ? parsedPort : 3000; }
        }

        private static string ServerIpAddress
        {
            // looks for ip address in config file, if it is empty localhost ip is used
            get 
            { 
                return ConfigurationManager.AppSettings.Get("IPADDRESS") ?? 
                    Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? 
                    throw new Exception("No network adapters with an IPv4 address in the system"); 
            }
        }
        
        // establishing tcp connection
        public static void ConnectTcpClient(string ipAddress, int port, out TcpClient client)
        {   
            client = new TcpClient();
            for (int i = 0; i < UTIL.NumberOfReconAttempts; i++)
            {
                try
                {
                    client.Connect(ipAddress, port);
                    if (client.Connected)
                    {
                        EventCollection.Log("Connected to the server.");
                        return;
                    }
                }
                catch (Exception)
                {
                    EventCollection.Log("Connection attempt failed. Retrying...");
                }
            }
            throw new Exception("Failed to establish a connection to the server. Ensure that the server is online and reachable.");
        }

        // process response payload
        static void Process() 
        {
            EventCollection.Log("--------- Process Started ---------");

            // create a list to store received packets
            List<Packet> receivedPackets = new();

            StreamAllPackets(receivedPackets);

            StreamMissingPackets(receivedPackets);
            

            EventCollection.Log("--------- Process Completed ---------");

            // generate a JSON file with the collected data
            GenerateJsonOutput(receivedPackets);

            EventCollection.Log("JSON Output Created");
        }
        

        static void StreamAllPackets(List<Packet> receivedPackets)
        {
            ConnectTcpClient(ServerIpAddress, ServerPort, out TcpClient client);

            using NetworkStream stream = client.GetStream();

            // send a request to stream all packets
            byte[] requestPayload = new byte[2];
            requestPayload[0] = 1; // Call Type 1 for "Stream All Packets"
            requestPayload[1] = 0;

            stream.Write(requestPayload, 0, requestPayload.Length);
            EventCollection.Log("Streaming all packets...");

            while (true)
            {
                byte[] buffer = new byte[UTIL.PACKETSIZE];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                    break; // Server closed the connection

                Packet packet = ParsePacket(buffer);
                if (ValidatePacket(packet)) 
                {
                    receivedPackets.Add(packet);
                }
            }

            EventCollection.Log("Received packets successfully. Total packets received: " + receivedPackets.Count);
        }
        
        static void StreamMissingPackets(List<Packet> receivedPackets)
        {
            List<int> missingSequences = FindMissingSequences(receivedPackets);

            ConnectTcpClient(ServerIpAddress, ServerPort, out TcpClient client);
            EventCollection.Log("Detected missing packets: " + missingSequences.Count);
            using (NetworkStream stream = client.GetStream())
            {
                foreach (int missingSeq in missingSequences)
                {
                    // Request missing packets one by one
                    byte[] resendRequest = CreateResendRequest(missingSeq);
                    stream.Write(resendRequest, 0, resendRequest.Length);

                    byte[] buffer = new byte[UTIL.PACKETSIZE];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                        break; 

                    Packet packet = ParsePacket(buffer);

                    if (ValidatePacket(packet)) 
                    {
                        receivedPackets.Add(packet);
                    }
                }
            }

            EventCollection.Log("Received missing packets successfully.");
            client.Close();
        }
        
        // parse a response packet and create a Packet object
        public static Packet ParsePacket(byte[] buffer)
        {
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

        // packet validation
        static bool ValidatePacket(Packet packet)
        {
            bool validSymbol = packet.Symbol.All(char.IsLetterOrDigit); // Check if Symbol contains only letters and digits
            bool validBuySellIndicator = (packet.BuySellIndicator == 'B' || packet.BuySellIndicator == 'S');

            if(packet.Quantity < 0 || packet.Price < 0 || !validSymbol || !validBuySellIndicator) 
            {
                EventCollection.Log("\tInvalid Packet Data. " + packet.GetHashCode());
                return false;
            }
            return true;
        }

        // find missing sequences in the received packets
        static List<int> FindMissingSequences(List<Packet> receivedPackets)
        {
            HashSet<int> set = new();
            List<int> missingSequences = new();
            int lastSeq = receivedPackets.Max(p => p.Sequence); // assumption: last sequence packet is never missed

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
