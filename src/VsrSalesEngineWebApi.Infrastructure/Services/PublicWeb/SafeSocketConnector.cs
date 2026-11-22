using System.Net;
using System.Net.Sockets;
using VsrSalesEngineWebApi.Core.Services.AgentRelay.People;

namespace VsrSalesEngineWebApi.Infrastructure.Services.PublicWeb;

/// <summary>
/// Opens the connection itself so the address that was checked is the address that is used. Without this a
/// hostile DNS server could answer a public address for the check and a private one for the real connection.
/// </summary>
public static class SafeSocketConnector
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || !addresses.All(SsrfGuard.IsPublicAddress))
            throw new HttpRequestException("Blocked: the host resolves to a non-public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
