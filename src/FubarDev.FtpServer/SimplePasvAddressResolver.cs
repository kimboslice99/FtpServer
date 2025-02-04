// <copyright file="SimplePasvAddressResolver.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FubarDev.FtpServer
{
    /// <summary>
    /// The default implementation of the <see cref="SimplePasvAddressResolver"/>.
    /// </summary>
    /// <remarks>
    /// The address family number gets ignored by this resolver. We always use the same
    /// address family as the local end point.
    /// </remarks>
    public class SimplePasvAddressResolver : IPasvAddressResolver
    {
        private static readonly string[] _ipv4Hosts = { "https://api.ipify.org", "https://ipv4.icanhazip.com" };
        private static readonly string[] _ipv6Hosts = { "https://api6.ipify.org", "https://ipv6.icanhazip.com" };
        private readonly SimplePasvOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimplePasvAddressResolver"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public SimplePasvAddressResolver(IOptions<SimplePasvOptions> options)
        {
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task<PasvListenerOptions> GetOptionsAsync(
            IFtpConnection connection,
            AddressFamily? addressFamily,
            CancellationToken cancellationToken)
        {
            var minPort = _options.PasvMinPort ?? 0;
            if (minPort > 0 && minPort < 1024)
            {
                minPort = 1024;
            }
            var remoteIPAddress = connection.RemoteEndPoint.Address;
            // only if not local, look up our public facing ip, this should handle most scenarios
            if(IsLocal(remoteIPAddress) is false)
            {
                var publicIp = await GetPublicIp(connection.RemoteEndPoint.AddressFamily);
                _options.PublicAddress = IPAddress.Parse(publicIp);
            }
            var maxPort = Math.Max(_options.PasvMaxPort ?? 0, minPort);
            var publicAddress = _options.PublicAddress ?? connection.LocalEndPoint.Address;

            return new PasvListenerOptions(minPort, maxPort, publicAddress);
        }

        internal async Task<HttpResponseMessage> GetASync(string uri)
        {
            using var client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(uri);
            return response;
        }

        internal async Task<string> GetPublicIp(System.Net.Sockets.AddressFamily family)
        {
            var ip = string.Empty;
            switch (family)
            {
                case AddressFamily.InterNetwork:
                    foreach (var host in _ipv4Hosts)
                    {
                        HttpResponseMessage response = await GetASync(host);
                        if(response != null && response.IsSuccessStatusCode)
                        {
                            ip = await response.Content.ReadAsStringAsync();
                            break;
                        }
                    }
                    break;
                case AddressFamily.InterNetworkV6:
                    foreach (var host in _ipv6Hosts)
                    {
                        HttpResponseMessage response = await GetASync(host);
                        if (response != null && response.IsSuccessStatusCode)
                        {
                            ip = await response.Content.ReadAsStringAsync();
                            break;
                        }
                    }
                    break;
                default:
                    throw new Exception("Invalid address family provided.");
            }
            return ip;
        }

        internal bool IsLocal(IPAddress remoteAddress)
        {
            if (remoteAddress.AddressFamily is AddressFamily.InterNetwork)
            {
                if (IsInIPv4Range(remoteAddress, "192.168.0.0/16"))
                    return true;
                if (IsInIPv4Range(remoteAddress, "0.0.0.0/8"))
                    return true;
                if (IsInIPv4Range(remoteAddress, "10.0.0.0/8"))
                    return true;
                if (IsInIPv4Range(remoteAddress, "172.16.0.0/12"))
                    return true;
                if (IsInIPv4Range(remoteAddress, "127.0.0.0/8"))
                    return true;
            }
            if(remoteAddress.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (IsIpv6InSubnet(remoteAddress, "fe80::", 10))
                    return true;
                if (IsIpv6InSubnet(remoteAddress, "fd00::", 8))
                    return true;
            }
            return false;
        }

        internal bool IsInIPv4Range(IPAddress ipAddress, string range)
        {
            string[] parts = range.Split('/');

            var iPaddr = BitConverter.ToInt32(ipAddress.GetAddressBytes(), 0);
            var cidrAddr = BitConverter.ToInt32(IPAddress.Parse(parts[0]).GetAddressBytes(), 0);
            var cidrMask = IPAddress.HostToNetworkOrder(-1 << (32 - int.Parse(parts[1])));

            return (iPaddr & cidrMask) == (cidrAddr & cidrMask);
        }

        internal bool IsIpv6InSubnet(IPAddress ip, string subnet, int prefix)
        {
            try
            {
                var ipv6Subnet = IPAddress.Parse(subnet).GetAddressBytes();
                var ipv6 = ip.GetAddressBytes();
                var ipv6Mask = GenerateIpv6Mask(prefix);

                for (int i = 0; i < 16; ++i)
                {
                    if ((ipv6[i] & ipv6Mask[i]) != (ipv6Subnet[i] & ipv6Mask[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] GenerateIpv6Mask(int prefix)
        {
            byte[] mask = new byte[16];
            int byteCount = prefix / 8;
            int bitCount = prefix % 8;

            for (int i = 0; i < byteCount; ++i)
            {
                mask[i] = 0xFF;  // Set whole byte
            }

            if (byteCount < 16)
            {
                mask[byteCount] = (byte)(0xFF << (8 - bitCount));  // Set partial byte
            }

            return mask;
        }
    }
}
