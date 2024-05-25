using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StyptoSlaveBot
{
    public class QuickMan
    {
        #region Server
        #region Server Init
        public Thread ServerThread;
        private TcpListener Listener;
        private IPAddress _Address;
        private int _Port = 1999;
        private Dictionary<string, string> _Routes;
        private static int _MaxSimultaneousConnections = 20;
        private Semaphore sem = new Semaphore(_MaxSimultaneousConnections, _MaxSimultaneousConnections);

        /// <summary>
        /// Starts server on localhost then returns server's address.
        /// </summary>
        /// <param name="Port">Port</param>
        /// <param name="Routes">Routes dictionary</param>
        /// <returns></returns>
        public string Start(int Port, Dictionary<string, string> Routes, int MaxSimultaneousConnections = 20)
        {
            _Port = Port;
            _Address = IPAddress.Parse(GetLocalIP());
            _Routes = Routes.OrderBy(r => r.Key == "*" ? 1 : 0).ToDictionary(x => x.Key, x => x.Value.Replace("127.0.0.1", _Address.ToString()));
            _MaxSimultaneousConnections = MaxSimultaneousConnections;
            ServerThread = new Thread(Listen) { IsBackground = false };
            ServerThread.Start();
            return $"http://{_Address}:{_Port}/";
        }
        private string GetLocalIP()
        {
            using (System.Net.Sockets.Socket Socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
            {
                Socket.BeginConnect("8.8.8.8", 65530, null, null).AsyncWaitHandle.WaitOne(500, true);
                return (Socket.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString();
            }
        }

        public void Stop()
        {
            ServerThread.Abort();
            Listener.Stop();
        }

        private void Listen()
        {
            try
            {
                Listener = new TcpListener(_Address, _Port);
                Listener.Start();
                Console.WriteLine($"INFO: Server running on {_Address}:{_Port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERR: Server failed to start.\nCause: {ex.Message}\nStackTrace:{ex.StackTrace}");
                return;
            }

            while (Listener != null)
            {
                try
                {
                    //sem.WaitOne();
                    TcpClient client = Listener.AcceptTcpClient();
                    new Thread(() => Process(client)) { IsBackground = true }.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERR: {ex.Message}");
                }
            }
        }

        private async void Process(TcpClient client)
        {
            try
            {
                using (var clientStream = client.GetStream())
                using (var reader = new StreamReader(clientStream))
                {
                    // Read the first line of the HTTP request to get the host
                    string requestLine = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(requestLine))
                    {
                        client.Close();
                        return;
                    }

                    // Read headers to find the Host header
                    string hostHeader = null;
                    List<string> headers = new List<string> { requestLine };
                    string line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        headers.Add(line);
                        if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                        {
                            hostHeader = line.Substring(5).Trim();
                        }
                    }

                    if (string.IsNullOrEmpty(hostHeader))
                    {
                        Console.WriteLine("ERR: No Host header found in the request.");
                        client.Close();
                        return;
                    }

                    string target = null;
                    foreach (var route in _Routes)
                    {
                        if (IsHostMatch(hostHeader, route.Key))
                        {
                            target = route.Value;
                            break;
                        }
                    }

                    if (target != null)
                    {
                        await ForwardTcpConnection(client, headers, clientStream, target, hostHeader);
                    }
                    else
                    {
                        client.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERR: {ex.Message}");
                client.Close();
            }
        }

        private bool IsHostMatch(string host, string pattern)
        {
            if (pattern == "*")
                return true;

            var hostParts = host.Split('.');
            var patternParts = pattern.Split('.');

            if (hostParts.Length != patternParts.Length)
            {
                return false;
            }

            for (int i = 0; i < hostParts.Length; i++)
            {
                if (patternParts[i] != "*" && patternParts[i] != hostParts[i])
                {
                    return false;
                }
            }

            return true;
        }

        private async Task ForwardTcpConnection(TcpClient client, List<string> headers, Stream clientStream, string target, string host)
        {
            string[] targetParts = target.Split(':');
            if (targetParts.Length != 2 || !IPAddress.TryParse(targetParts[0], out var targetIP) || !int.TryParse(targetParts[1], out var targetPort))
            {
                Console.WriteLine($"ERR: Invalid target format - {target}");
                client.Close();
                return;
            }

            using (var targetClient = new TcpClient())
            {
                try
                {
                    await targetClient.ConnectAsync(targetIP, targetPort);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERR: Failed to connect to target - {target}. Exception: {ex.Message}");
                    client.Close();
                    return;
                }

                using (var targetStream = targetClient.GetStream())
                using (var writer = new StreamWriter(targetStream, Encoding.ASCII) { AutoFlush = true })
                {
                    // Log the forwarding action
                    Console.WriteLine($"Forwarding connection: [{host}] -> [{targetIP}:{targetPort}]");

                    // Write headers to the target
                    foreach (var header in headers)
                    {
                        await writer.WriteLineAsync(header);
                    }
                    await writer.WriteLineAsync(); // End of headers

                    // Forward the remaining data
                    var clientToTarget = CopyStreamAsync(clientStream, targetStream);
                    var targetToClient = CopyStreamAsync(targetStream, clientStream);

                    await Task.WhenAll(clientToTarget, targetToClient);

                    // Close streams and clients only after both tasks are complete
                    clientStream.Close();
                    targetStream.Close();
                    client.Close();
                    targetClient.Close();
                }
            }
        }

        private async Task CopyStreamAsync(Stream source, Stream destination)
        {
            byte[] buffer = new byte[8192];
            int bytesRead;
            try
            {
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERR: Stream copy error. Exception: {ex.Message}");
            }
        }
        #endregion
        #endregion
    }
}
