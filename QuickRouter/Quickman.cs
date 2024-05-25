using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
                    sem.WaitOne();
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
                string host = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                string target = null;

                foreach (var route in _Routes)
                {
                    if (IsHostMatch(host, route.Key))
                    {
                        target = route.Value;
                        break;
                    }
                }

                if (target != null)
                {
                    await ForwardTcpConnection(client, target);
                }
                else
                {
                    client.Close();
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

        private async Task ForwardTcpConnection(TcpClient client, string target)
        {
            var targetUri = new Uri(target);
            using (var targetClient = new TcpClient())
            {
                await targetClient.ConnectAsync(targetUri.Host, targetUri.Port);
                using (var clientStream = client.GetStream())
                using (var targetStream = targetClient.GetStream())
                {
                    Console.WriteLine($"Forwarding connection: [{client.Client.RemoteEndPoint}] -> [{targetUri}]");

                    var clientToTarget = clientStream.CopyToAsync(targetStream);
                    var targetToClient = targetStream.CopyToAsync(clientStream);

                    await Task.WhenAny(clientToTarget, targetToClient);
                }
            }
        }
        #endregion
        #endregion
    }
}
