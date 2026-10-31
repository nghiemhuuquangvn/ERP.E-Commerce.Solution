using System.Net;
using System.Net.Sockets;

namespace ERP.Shared.Security;

/// <summary>
/// SSRF policy for tenant-configured webhook URLs (shared by Identity and Notification, ROADMAP P2-1b). A webhook URL is chosen by a tenant
/// admin and then fetched server-side from inside the platform network,
/// so without this policy it could target internal hosts (sqlserver, other services,
/// cloud metadata at 169.254.169.254, ...). Enforced twice: statically when the endpoint is
/// saved (validators), and at connect time against the actually-resolved IPs
/// (the sender's ConnectCallback — defeats DNS names that resolve/rebind to internal IPs).
/// </summary>
public static class WebhookUrlRules
{
    private static readonly string[] BlockedHostSuffixes = [".local", ".internal", ".localhost", ".lan", ".home", ".corp"];

    /// <summary>Static checks on the URL itself (no DNS): https, public-looking host, no blocked IP literal.</summary>
    public static bool IsAllowedUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return false;

        var host = uri.IdnHost.TrimEnd('.');
        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
            return !IsBlockedAddress(literal);

        // Single-label names ("sqlserver", "identityservice") are Docker/K8s service names.
        if (!host.Contains('.') || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        return !BlockedHostSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Loopback, private (RFC 1918 / ULA), link-local, CGNAT, multicast, unspecified, etc.</summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast) || address.Equals(IPAddress.IPv6None))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (address.GetAddressBytes()[0] & 0xFE) == 0xFC; // fc00::/7 unique-local

        var b = address.GetAddressBytes();
        return b[0] == 0                                   // 0.0.0.0/8
            || b[0] == 10                                  // 10.0.0.0/8
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // 100.64.0.0/10 CGNAT
            || b[0] == 127                                 // loopback
            || (b[0] == 169 && b[1] == 254)                // link-local / cloud metadata
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12
            || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16
            || (b[0] == 192 && b[1] == 0 && b[2] == 0)     // 192.0.0.0/24
            || (b[0] == 198 && (b[1] == 18 || b[1] == 19)) // 198.18.0.0/15 benchmarking
            || b[0] >= 224;                                // multicast + reserved
    }
}
