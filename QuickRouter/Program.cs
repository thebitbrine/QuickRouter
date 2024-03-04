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

            routes.Add("trade.styp.to", "127.0.0.1:7474");
            routes.Add("xyx.styp.to", "127.0.0.1:8080");
            routes.Add("eternal.styp.to", "127.0.0.1:8080");
            routes.Add("*.styp.to", "127.0.0.1:808");
            routes.Add("*", "127.0.0.1:808");


            API.Start(80, routes, 66666);
        }
    }
}
