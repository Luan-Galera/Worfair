using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Worfair.Api.Infrastructure;
using Xunit;

namespace Worfair.Tests.Architecture;

public sealed class HostUrlResolverTests
{
    [Fact]
    public void ResolveUrls_UsesExplicitConfiguration_WhenProvided()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_URLS"] = "http://127.0.0.1:6100"
            })
            .Build();

        HostUrlResolver.ResolveUrls(configuration).Should().Be("http://127.0.0.1:6100");
    }

    [Fact]
    public void ResolveUrls_ChoosesNextAvailablePort_WhenPreferredPortIsBusy()
    {
        var preferredPort = GetFreePort();
        using var listener = new TcpListener(IPAddress.Loopback, preferredPort);
        listener.Start();

        var configuration = new ConfigurationBuilder().Build();
        var url = HostUrlResolver.ResolveUrls(configuration, preferredPort);
        var fallbackPort = int.Parse(url.Replace("http://127.0.0.1:", string.Empty));

        fallbackPort.Should().BeGreaterThan(preferredPort);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
