// SPDX-License-Identifier: MIT
// ASP.NET Core WebApplicationFactory tuned for the API server tests.
// - Picks a random local UDP port so tests can run in parallel without
//   colliding on 4242.
// - Overrides the listen address to "127.0.0.1" so the test never
//   touches a public interface.
// - Exposes a helper that waits for the UDP listener to bind before
//   returning control to test code.

using System.Net;
using System.Net.Sockets;
using CS2M.ApiServer.Protocol.Udp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CS2M.ApiServer.Tests.Helpers;

public sealed class ApiServerWebApplicationFactory : WebApplicationFactory<Program>
{
    public int UdpPort { get; } = FindFreeUdpPort();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Udp:Port"] = UdpPort.ToString(),
                ["Udp:BindAddress"] = "127.0.0.1",
                ["Urls"] = "http://127.0.0.1:0"
            });
        });
    }

    public async Task WaitForUdpListenerAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var listener = Services.GetRequiredService<ApiUdpListener>();
        while (DateTime.UtcNow < deadline)
        {
            if (listener.LocalEndpoint is not null)
            {
                return;
            }
            await Task.Delay(50).ConfigureAwait(false);
        }
        throw new TimeoutException("UDP listener did not bind within timeout.");
    }

    private static int FindFreeUdpPort()
    {
        var probe = new UdpClient(0);
        try
        {
            return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
        }
        finally
        {
            probe.Close();
        }
    }
}