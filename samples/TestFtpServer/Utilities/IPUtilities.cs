using System;
using System.Net;
using System.Net.Sockets;

namespace TestFtpServer.Utilities
{
    public class IPUtilities
    {
        public bool IsLocal(IPAddress remoteAddress)
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
            if (remoteAddress.AddressFamily == AddressFamily.InterNetworkV6)
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
                // Convert the subnet and IP to byte arrays
                var ipv6Subnet = IPAddress.Parse(subnet).GetAddressBytes();
                var ipv6 = ip.GetAddressBytes();

                // Generate the mask based on the prefix length
                var ipv6Mask = GenerateIpv6Mask(prefix);

                // Compare the IP address and the subnet with the mask
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
