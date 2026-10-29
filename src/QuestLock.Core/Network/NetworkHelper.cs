using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace QuestLock.Core.Network;

public class NetworkInfo
{
    public string? LocalLanIp { get; set; }
    public string? TailscaleIp { get; set; }
    public bool HasTailscale => !string.IsNullOrEmpty(TailscaleIp);
}

public static class NetworkHelper
{
    public static NetworkInfo GetActiveNetworkInfo()
    {
        var result = new NetworkInfo();

        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;

                var isTailscaleInterface = ni.Name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) ||
                                           ni.Description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase);

                var ipProps = ni.GetIPProperties();
                foreach (var ua in ipProps.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                    var addr = ua.Address.ToString();
                    if (addr.StartsWith("169.254.") || addr == "127.0.0.1") continue;

                    // Tailscale IP adresy jsou v rozsahu 100.64.0.0/10 (100.64.x.x az 100.127.x.x)
                    if (isTailscaleInterface || (addr.StartsWith("100.") && int.TryParse(addr.Split('.')[1], out var secondOctet) && secondOctet >= 64 && secondOctet <= 127))
                    {
                        result.TailscaleIp = addr;
                    }
                    else if (result.LocalLanIp == null)
                    {
                        result.LocalLanIp = addr;
                    }
                }
            }
        }
        catch
        {
            // Ochrana pri selhani dotazu na sitove adaptery
        }

        return result;
    }
}
