using System.Net;
using System.Net.Sockets;
using VsrSalesEngineWebApi.Core.Interfaces;

namespace VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

/// <summary>Refuses anything that is not a plain public web address: private, loopback, link-local and metadata ranges.</summary>
public static class SsrfGuard
{
    public static bool IsAllowedUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
        if (uri.Port != 80 && uri.Port != 443) return false;
        if (string.IsNullOrWhiteSpace(uri.Host)) return false;
        return !IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) || IsPublicAddress(literal);
    }

    /// <summary>True only when the host resolves, and every address it resolves to is public.</summary>
    public static async Task<bool> IsSafeAsync(Uri uri, IHostResolver resolver, CancellationToken cancellationToken = default)
    {
        if (!IsAllowedUri(uri)) return false;
        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal)) return IsPublicAddress(literal);

        var addresses = await resolver.ResolveAsync(uri.Host, cancellationToken);
        return addresses.Count > 0 && addresses.All(IsPublicAddress);
    }

    public static bool IsPublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;

        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork) return IsPublicV4(b);
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return false;

        if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return false;
        if ((b[0] & 0xFE) == 0xFC) return false;                                  // fc00::/7 unique local
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && b[4..12].All(x => x == 0))
            return IsPublicV4(b[12..16]);                                          // 64:ff9b::/96 NAT64
        return true;
    }

    private static bool IsPublicV4(byte[] b) => !(
        b[0] == 0 || b[0] == 10 || b[0] == 127 ||
        (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||        // 100.64.0.0/10 carrier-grade NAT
        (b[0] == 169 && b[1] == 254) ||                      // link-local, incl. cloud metadata 169.254.169.254
        (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
        (b[0] == 192 && b[1] == 168) ||
        (b[0] == 192 && b[1] == 0 && b[2] == 0) ||
        (b[0] == 198 && (b[1] == 18 || b[1] == 19)) ||
        b[0] >= 224);                                        // multicast and reserved
}
