using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ColinChang.IpMaskConverter;

namespace Colin.WebServerFinder
{
    public class WebServerFinder
    {
        private readonly List<string> _ips;
        private ConcurrentBag<string> _result;

        public int[] Ports { get; set; }
        public Predicate<string> Filter { get; set; }
        private bool IncludingHttps { get; set; }
        private bool SkipDns { get; set; }
        public int MaxThreads { get; set; }
        public TimeSpan Timeout { get; set; }

        public WebServerFinder(string ipRange) : this(ipRange, new[] {80},
            response => response.ToLower().StartsWith("<!doctype html>"), TimeSpan.FromSeconds(2))
        {
        }

        public WebServerFinder(string startIp, string endIp) : this(startIp, endIp, new[] {80},
            response => response.ToLower().StartsWith("<!doctype html>"), TimeSpan.FromSeconds(2))
        {
        }

        public WebServerFinder(string ipRange, Predicate<string> filter) : this(ipRange, new[] {80},
            filter, TimeSpan.FromSeconds(2))
        {
        }

        public WebServerFinder(string startIp, string endIp, Predicate<string> filter) : this(startIp, endIp,
            new[] {80},
            filter, TimeSpan.FromSeconds(2))
        {
        }


        /// <summary>
        /// Create a WebServerFinder
        /// </summary>
        /// <param name="startIp">start ip address</param>
        /// <param name="endIp">end ip address</param>
        /// <param name="ports">ports listened on web servers</param>
        /// <param name="filter">rules to filter the results</param>
        /// <param name="timeoutPerServer">timeout for every server</param>
        /// <param name="includingHttps">whether include HTTPS protocol</param>
        /// <param name="skipDns">whether skip DNS server.it is the ip end with 1,like 192.168.0.1</param>
        /// <param name="maxThreads">maximum threads to run together</param>
        public WebServerFinder(string startIp, string endIp, int[] ports, Predicate<string> filter,
            TimeSpan timeoutPerServer,
            bool includingHttps = false, bool skipDns = true,
            int maxThreads = 8)
        {
            _ips=new List<string>();
            for (var ip = startIp.ToIpNumber(); ip <= endIp.ToIpNumber(); ip++)
                _ips.Add(ip.ToIpAddress());

            Ports = ports;
            Filter = filter;
            Timeout = timeoutPerServer;
            IncludingHttps = includingHttps;
            SkipDns = skipDns;
            MaxThreads = maxThreads;
        }

        /// <summary>
        /// Create a WebServerFinder
        /// </summary>
        /// <param name="ipRange">ip range to search.it should use "ip/mask" like "192.168.0.0/24"</param>
        /// <param name="ports">ports listened on web servers</param>
        /// <param name="filter">rules to filter the results</param>
        /// <param name="timeoutPerServer">timeout for every server</param>
        /// <param name="includingHttps">whether include HTTPS protocol</param>
        /// <param name="skipDns">whether skip DNS server.it is the ip end with 1,like 192.168.0.1</param>
        /// <param name="maxThreads">maximum threads to run together</param>
        public WebServerFinder(string ipRange, int[] ports, Predicate<string> filter, TimeSpan timeoutPerServer,
            bool includingHttps = false,
            bool skipDns = true,
            int maxThreads = 8)
        {
            _ips = ipRange.ToIpList();

            Ports = ports;
            Filter = filter;
            Timeout = timeoutPerServer;
            IncludingHttps = includingHttps;
            SkipDns = skipDns;
            MaxThreads = maxThreads;
        }

        /// <summary>
        /// search the specify ip range to find web servers available.
        /// </summary>
        /// <returns>web servers available</returns>
        public async Task<IEnumerable<string>> FindAsync()
        {
            return await Task.Run(() =>
            {
                _result = new ConcurrentBag<string>();

                var queue = new ConcurrentQueue<string>();
                var are = new AutoResetEvent(false);
                foreach (var ip in _ips)
                {
                    if (SkipDns && ip.Split('.').LastOrDefault() == "1")
                        continue;

                    foreach (var port in Ports)
                    {
                        queue.Enqueue($"http://{ip}:{port}");

                        if (IncludingHttps)
                            queue.Enqueue($"https://{ip}:{port}");
                    }
                }

                Find(queue, are);
                are.WaitOne();
                return _result;
            });
        }

        private void Find(ConcurrentQueue<string> queue, EventWaitHandle are)
        {
            var hc = new HttpClient {Timeout = Timeout};
            var locker = new Locker(MaxThreads);

            for (var i = 0; i < MaxThreads; i++)
            {
                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    while (queue.Count > 0)
                    {
                        if (!queue.TryDequeue(out var url))
                            await Task.Delay(200);

                        try
                        {
                            var response = await hc.GetStringAsync(url);
                            if (string.IsNullOrWhiteSpace(response))
                                continue;

                            if (!Filter(response))
                                continue;

                            
                            if (url.StartsWith("https"))
                            {
                                var index = url.LastIndexOf(":", StringComparison.Ordinal);
                                var port = url.Substring(index);
                                url = port == ":443" ? url.Substring(0, index) : url;
                            }
                            else
                            {
                                var index = url.LastIndexOf(":", StringComparison.Ordinal);
                                var port = url.Substring(index);
                                url = port == ":80" ? url.Substring(0, index) : url;
                            }

                            _result.Add(url);
                            Console.Write("#");
                        }
                        catch
                        {
                            Console.Write("#");
                        }
                    }

                    lock (locker)
                    {
                        locker.Done++;
                        if (!locker.Finished)
                            return;

                        hc.Dispose();
                        are.Set();
                    }
                });
            }
        }

        private class Locker
        {
            public int Done { get; set; }

            public bool Finished => Done >= _max;

            private readonly int _max;
            public Locker(int max) => _max = max;
        }
    }
}