using StyptoSlaveBot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace QuickRouter
{
    internal class Program
    {
        private static QuickMan API;
        static void Main(string[] args)
        {
            API = new QuickMan();
            var routes = new Dictionary<string, string>();

            routes.Add("*", "api.ipify.org");
            routes.Add("*.example.com", "google.com");
            routes.Add("test.example.com", "127.0.0.1:8089");


            API.Start(80, routes, 66666);
        }
    }
}
