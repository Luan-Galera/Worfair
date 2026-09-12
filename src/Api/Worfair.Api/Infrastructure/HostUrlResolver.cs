using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;

namespace Worfair.Api.Infrastructure;

public static class HostUrlResolver
{
    public static string ResolveUrls(IConfiguration configuration, int preferredPort = 5000)
    {
        var configured = configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var candidatePort = preferredPort;
        while (candidatePort < preferredPort + 50)
        {
            var url = $"http://127.0.0.1:{candidatePort}";
            if (IsPortAvailable(candidatePort))
                return url;

            candidatePort++;
        }

        return $"http://127.0.0.1:{preferredPort}";
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
