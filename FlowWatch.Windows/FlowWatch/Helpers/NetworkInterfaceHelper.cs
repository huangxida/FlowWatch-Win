using System.Linq;
using System.Net.NetworkInformation;

namespace FlowWatch.Helpers
{
    public static class NetworkInterfaceHelper
    {
        public static NetworkInterface GetActiveInterface()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Prefer an up, non-loopback interface with the most traffic
            var candidates = interfaces
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToArray();

            if (candidates.Length == 0) return null;

            // Prefer Ethernet or Wi-Fi
            var preferred = candidates
                .Where(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                    || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                .OrderByDescending(ni =>
                {
                    var stats = ni.GetIPv4Statistics();
                    return stats.BytesReceived + stats.BytesSent;
                })
                .FirstOrDefault();

            return preferred ?? candidates
                .OrderByDescending(ni =>
                {
                    try
                    {
                        var stats = ni.GetIPv4Statistics();
                        return stats.BytesReceived + stats.BytesSent;
                    }
                    catch
                    {
                        return 0L;
                    }
                })
                .First();
        }
    }
}
