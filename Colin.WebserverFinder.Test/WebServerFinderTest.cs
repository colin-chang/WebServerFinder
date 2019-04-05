using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Colin.WebServerFinder.Test
{
    public class WebServerFinderTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public WebServerFinderTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task FindTestAsync()
        {
            var finder = new WebServerFinder("192.168.31.0/24") {MaxThreads = 16};
            var servers = await finder.FindAsync();
            foreach (var server in servers)
            {
                _testOutputHelper.WriteLine(server);
            }
        }
    }
}