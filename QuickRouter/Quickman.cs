using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;

namespace StyptoSlaveBot
{
    public class QuickMan
    {
        #region Server
        #region Server Init
        public Thread ServerThread;
        private HttpListener Listener;
        private IPAddress _Address;
        private int _Port = 1999;
        private Dictionary<string, string> _Routes;

        private static int _MaxSimultaneousConnections = 20;
        private Semaphore sem = new Semaphore(_MaxSimultaneousConnections, _MaxSimultaneousConnections);

        /// <summary>
        /// Starts server then returns server's address.
        /// </summary>
        /// <param name="Address">IP Address</param>
        /// <param name="Port">Port</param>
        /// <param name="Routes">Routes dictionary</param>
        /// <returns></returns>
        public string Start(IPAddress Address, int Port, Dictionary<string, string> Routes, int MaxSimultaneousConnections = 20)
        {
            _Port = Port;
            _Address = Address;
            _Routes = Routes.OrderBy(r => r.Key == "*" ? 1 : 0).ToDictionary(x => x.Key, x => x.Value.Replace("127.0.0.1", _Address.ToString()));
            _MaxSimultaneousConnections = MaxSimultaneousConnections;
            ServerThread = new Thread(Listen) { IsBackground = false };
            ServerThread.Start();
            return $"http://{_Address}:{_Port}/";
        }

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

        /// <summary>
        /// Starts server on localhost:1999 then returns server's address.
        /// </summary>
        /// <param name="Routes">Routes dictionary</param>
        /// <returns></returns>
        public string Start(Dictionary<string, string> Routes, int MaxSimultaneousConnections = 20)
        {
            _Address = IPAddress.Parse(GetLocalIP());
            _Routes = Routes.OrderBy(r => r.Key == "*" ? 1 : 0).ToDictionary(x => x.Key, x => x.Value.Replace("127.0.0.1", _Address.ToString()));
            _MaxSimultaneousConnections = MaxSimultaneousConnections;
            ServerThread = new Thread(Listen) { IsBackground = false };
            ServerThread.Start();
            return $"http://{_Address}:{_Port}/";
        }

        /// <summary>
        /// Stops server.
        /// </summary>
        public void Stop()
        {
            ServerThread.Abort();
            Listener.Stop();
        }

        private void Listen()
        {
            try
            {
                string Address = $"http://{_Address}:{_Port}/";
                AllowListener(Address);
                Listener = new HttpListener();
                HttpListenerTimeoutManager manager;
                manager = Listener.TimeoutManager;
                manager.IdleConnection = TimeSpan.FromMinutes(5);
                manager.HeaderWait = TimeSpan.FromMinutes(5);
                manager.EntityBody = TimeSpan.FromMilliseconds(Timeout.Infinite);
                manager.DrainEntityBody = TimeSpan.FromMilliseconds(Timeout.Infinite);
                manager.MinSendBytesPerSecond = 0;
                Listener.Prefixes.Add(Address);

                Listener.Start();
                PrintLine($"INFO: Server running on {Address}");
            }
            catch (Exception ex)
            {
                PrintLine($"ERR: Server failed to start.\nCause: {ex.Message}\nStackTrace:{ex.StackTrace}");
            }

            while (Listener != null)
            {
                try
                {
                    //sem.WaitOne();
                    HttpListenerContext context = Listener.GetContext();
                    new Thread(() => Process(context)) { IsBackground = true }.Start();
                }
                catch (Exception ex)
                {
                    PrintLine($"ERR: {ex.Message}");
                }
            }
        }
        #endregion
        #region Server Misc.

        private void PrintLine(string String)
        {
            Console.WriteLine(Tag(String));
        }

        private string Tag(string Text)
        {
            return "[" + DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString() + "] " + Text;
        }

        private string GetLocalIP()
        {
            using (System.Net.Sockets.Socket Socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, 0))
            {
                Socket.BeginConnect("8.8.8.8", 65530, null, null).AsyncWaitHandle.WaitOne(500, true);
                return (Socket.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString();
            }
        }

        public void AllowListener(string URL)
        {
            try
            {
                string command = $"http add urlacl url={new Uri(URL).AbsoluteUri} user=Everyone";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("netsh", command) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Verb = "runas" });
            }
            catch (Exception ex) {  }
        }

        public void Respond(string Response, HttpListenerContext Context)
        {
            try
            {
                Stream Input = StringToStream(Response);
                Context.Response.ContentType = "application/json";
                Context.Response.ContentLength64 = Input.Length;
                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = Input.Read(buffer, 0, buffer.Length)) > 0)
                    Context.Response.OutputStream.Write(buffer, 0, nbytes);
                Input.Close();
                Context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch
            {
                Context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            Context.Response.OutputStream.Flush();
        }

        public void Respond(string Response, string ContentType, HttpListenerContext Context)
        {
            try
            {
                Stream Input = StringToStream(Response);
                Context.Response.ContentType = ContentType;
                Context.Response.ContentLength64 = Input.Length;
                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = Input.Read(buffer, 0, buffer.Length)) > 0)
                    Context.Response.OutputStream.Write(buffer, 0, nbytes);
                Input.Close();
                Context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch
            {
                Context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            Context.Response.OutputStream.Flush();
        }

        public void Respond(Stream Response, string ContentType, HttpListenerContext Context)
        {
            try
            {
                Response.Position = 0;
                Context.Response.ContentType = ContentType;
                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = Response.Read(buffer, 0, buffer.Length)) > 0)
                    Context.Response.OutputStream.Write(buffer, 0, nbytes);
                Response.Close();
                Context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch
            {
                Context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            Context.Response.OutputStream.Flush();
        }

        public void Respond(FileStream Response, string ContentType, HttpListenerContext Context)
        {
            try
            {
                Response.Position = 0;
                Context.Response.ContentType = ContentType;
                byte[] buffer = new byte[1024 * 16];
                int nbytes;
                while ((nbytes = Response.Read(buffer, 0, buffer.Length)) > 0)
                    Context.Response.OutputStream.Write(buffer, 0, nbytes);
                Response.Close();
                Context.Response.StatusCode = (int)HttpStatusCode.OK;
            }
            catch
            {
                Context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            Context.Response.OutputStream.Flush();
        }

        private Stream StringToStream(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
        #endregion
        #region Server Main
        private void Process(HttpListenerContext context)
        {
            try
            {
                string host = context.Request.Url.Host;
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
                    ForwardRequest(context, target).Wait();
                }
                else
                {
                    context.Response.StatusDescription = "Endpoint not found";
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.OutputStream.Flush();
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.StatusDescription = ex.Message;
                context.Response.OutputStream.Flush();
            }
            finally
            {
                context.Response.OutputStream.Close();
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
        private async Task ForwardRequest(HttpListenerContext context, string target)
        {
            var tempUri = new UriBuilder(target);
            var targetUri = new UriBuilder(context.Request.Url) { Host = tempUri.Host, Port = tempUri.Port }.Uri;

            var client = new RestClient(targetUri);

            // Determine the appropriate method
            var method = new HttpMethod(context.Request.HttpMethod);
            //var restRequestMethod = Method.GET; // Default to GET
            Enum.TryParse(method.Method, true, out Method restRequestMethod);

            //switch (method.Method)
            //{
            //    case "POST":
            //        restRequestMethod = Method.POST;
            //        break;
            //    case "PUT":
            //        restRequestMethod = Method.PUT;
            //        break;
            //    case "DELETE":
            //        restRequestMethod = Method.DELETE;
            //        break;
            //    case "DELETE":
            //        restRequestMethod = Method.PATCH;
            //        break;
            //}

            var request = new RestRequest(restRequestMethod);

            //Copy headers from the original request
            foreach (string headerKey in context.Request.Headers)
            {
                if (headerKey == "Host")
                {
                    request.AddHeader(headerKey, targetUri.Host);
                    continue;
                }
                if (headerKey == "Referer")
                {
                    request.AddHeader(headerKey, targetUri.ToString());
                    continue;
                }
                if (headerKey == "Origin")
                {
                    request.AddHeader(headerKey, $"{targetUri.Scheme}://{targetUri.Host}");
                    continue;
                }
                request.AddHeader(headerKey, context.Request.Headers[headerKey]);
            }

            // Add body if applicable
            if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    var body = await reader.ReadToEndAsync();
                    string contentType = context.Request.ContentType ?? "application/json"; // Fallback to "application/json" if ContentType is null
                    request.AddParameter(contentType, body, ParameterType.RequestBody);
                }
            }

            // Execute the request
            var response = await client.ExecuteAsync(request);

            // Relay response back to original client
            if (Enum.IsDefined(typeof(HttpStatusCode), response.StatusCode))
            {
                context.Response.StatusCode = (int)response.StatusCode;
            }
            else
            {
                // Handle the case where the status code is not valid
                // You might set a default status code or handle this as an error
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
            foreach (var header in response.Headers)
            {
                if (!WebHeaderCollection.IsRestricted(header.Name))
                    context.Response.Headers[header.Name] = header.Value.ToString();
            }

            using (var writer = new StreamWriter(context.Response.OutputStream))
            {
                await writer.WriteAsync(response.Content);
            }
            PrintLine($"Forwarded: [{context.Request.Url}] -> [{targetUri}]");
        }
        private async Task ForwardRequestHttpClient(HttpListenerContext context, string target)
        {
            using (var client = new HttpClient())
            {
                var currentPath = new UriBuilder(context.Request.Url).Path;
                var targetUri = new UriBuilder(target + currentPath).Uri;
                var requestMessage = new HttpRequestMessage
                {
                    RequestUri = targetUri,
                    Method = new HttpMethod(context.Request.HttpMethod)
                };

                if (context.Request.HttpMethod == "POST" || context.Request.HttpMethod == "PUT" || context.Request.HttpMethod == "DELETE" || context.Request.HttpMethod == "PATCH")
                    requestMessage.Content = new StreamContent(context.Request.InputStream);

                foreach (string headerKey in context.Request.Headers)
                {
                    requestMessage.Headers.TryAddWithoutValidation(headerKey, context.Request.Headers[headerKey]);
                }
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                var responseMessage = await client.SendAsync(requestMessage);
                context.Response.StatusCode = (int)responseMessage.StatusCode;
                foreach (var header in responseMessage.Headers)
                {
                    if (!WebHeaderCollection.IsRestricted(header.Key))
                        context.Response.Headers[header.Key] = string.Join(",", header.Value);
                }

                using (var responseStream = await responseMessage.Content.ReadAsStreamAsync())
                {
                    await responseStream.CopyToAsync(context.Response.OutputStream);
                }
            }
        }
        #endregion
        #endregion

    }
}