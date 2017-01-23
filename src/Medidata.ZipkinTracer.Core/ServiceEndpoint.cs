using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Medidata.ZipkinTracer.Models;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core
{
    public class ServiceEndpoint 
    {
        public virtual async Task<Endpoint> GetLocalEndpoint(string serviceName, ushort port)
        {
            return new Endpoint()
            {
                ServiceName = serviceName,
                IPAddress = await GetLocalIPAddress(),
                Port = port
            };
        }

        public virtual async Task<Endpoint> GetRemoteEndpoint(Uri remoteServer, string remoteServiceName)
        {
            var address = await GetRemoteIPAddress(remoteServer);
            var addressBytes = address.GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(addressBytes);
            }

            var ipAddressStr = BitConverter.ToInt32(addressBytes, 0);
            var hostIpAddressStr = (int)IPAddress.HostToNetworkOrder(ipAddressStr);

            return new Endpoint()
            {
                ServiceName = remoteServiceName,
                IPAddress = await GetRemoteIPAddress(remoteServer),
                Port = (ushort)remoteServer.Port
            };
        }

        private static async Task<IPAddress> GetLocalIPAddress()
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return null;
            }

            IPHostEntry host = await Dns.GetHostEntryAsync(Dns.GetHostName());

            return host
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }

        private static async Task<IPAddress> GetRemoteIPAddress(Uri remoteServer)
        {
            var adressList = await Dns.GetHostAddressesAsync(remoteServer.Host);
            return adressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }
    }
}
