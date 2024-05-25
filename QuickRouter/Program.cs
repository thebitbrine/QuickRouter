using StyptoSlaveBot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace QuickRouter
{
    internal class Program
    {
        private static QuickMan API;
        static void Main(string[] args)
        {
            API = new QuickMan();

            Console.Title = "QuickRouter v2.2";
            var routes = new Dictionary<string, string>();
            if (File.Exists("Routes.txt"))
            {
                Console.WriteLine("Loading routes from file...");
                var txt = File.ReadAllLines("Routes.txt");
                foreach (var line in txt)
                {
                    if (line.Contains(">"))
                    {
                        var split = line.Split('>');
                        var key = split[0].Trim();
                        var value = split[1].Trim();
                        routes.Add(key, value);
                    }
                }
                Console.WriteLine($"Loaded {txt.Count()} routes.");
            }
            else
            {

                Console.WriteLine("Starting up using default routes...");
                routes.Add("trade.styp.to", "127.0.0.1:7474");
                routes.Add("xyx.styp.to", "127.0.0.1:8080");
                routes.Add("eternal.styp.to", "127.0.0.1:8080");
                routes.Add("*.styp.to", "127.0.0.1:808");
                routes.Add("*", "127.0.0.1:808");

                var txt = "";
                foreach (var route in routes)
                {
                    txt += $"{route.Key}\t>\t{route.Value}\n";
                }

                File.WriteAllText("Routes.txt", txt);
                Thread.Sleep(1111);
            }


            Console.WriteLine("==============LOADED ROUTES==============");
            foreach (var route in routes)
            {
                Console.WriteLine($"{route.Key}\t->\t{route.Value}");
            }
            Console.WriteLine("=========================================");
            Console.WriteLine();

            API.Start(80, routes, 66666);
        }
    }
}
