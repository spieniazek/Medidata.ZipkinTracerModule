using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core.Test
{
    [TestClass]
    public class ZipkinEndpointTests
    {

        [TestMethod]
        public async Task GetLocalEndpoint()
        {
            var serviceName = "name";
            ushort port = 12312;

            var zipkinEndpoint = new ServiceEndpoint();
            var endpoint = await zipkinEndpoint.GetLocalEndpoint(serviceName, port);

            Assert.IsNotNull(endpoint);
            Assert.AreEqual(serviceName, endpoint.ServiceName);
            Assert.IsNotNull(endpoint.IPAddress);
            Assert.IsNotNull(endpoint.Port);
        }

        [TestMethod]
        public async Task GetRemoteEndpoint()
        {
            var remoteUri = new Uri("http://localhost");
            var serviceName = "name";

            var zipkinEndpoint = new ServiceEndpoint();
            var endpoint = await zipkinEndpoint.GetRemoteEndpoint(remoteUri, serviceName);

            Assert.IsNotNull(endpoint);
            Assert.AreEqual(serviceName, endpoint.ServiceName);
            Assert.IsNotNull(endpoint.IPAddress);
            Assert.IsNotNull(endpoint.Port);
        }
    }
}
