using System;
using System.Linq;
using Colin.WebServerFinder;

namespace ColinChang.WebserverFinder.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("start detecting...");

            var finder = new WebServerFinder("192.168.31.0/24") {MaxThreads = 16,Timeout = TimeSpan.FromSeconds(1)};
            //var finder = new WebServerFinder("192.168.31.200","192.168.31.255") {MaxThreads = 16};
            
            var servers = finder.FindAsync().Result;

            Console.WriteLine($"\r\nall done.{(servers.Any() ? "here are addresses available" : "there is no server available")} ...");
            foreach (var server in servers)
                Console.WriteLine(server);

            Console.WriteLine("press any key to exit...");
            Console.ReadKey();
        }
    }
}