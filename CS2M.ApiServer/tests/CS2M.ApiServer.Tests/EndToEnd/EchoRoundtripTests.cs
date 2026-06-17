// SPDX-License-Identifier: MIT
// End-to-end test: spin up the API server, send a
// ServerRegistrationCommand from a test UDP client, assert that the
// matching PortCheckResultCommand comes back.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Udp;
using CS2M.ApiServer.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CS2M.ApiServer.Tests.EndToEnd;

public class EchoRoundtripTests : IClassFixture<ApiServerWebApplicationFactory>
{
    private readonly ApiServerWebApplicationFactory _factory;

    public EchoRoundtripTests(ApiServerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ServerRegistrationCommand_yields_Reachable_reply()
    {
        // Ensure the listener has bound before we send.
        var listener = _factory.Services.GetRequiredService<ApiUdpListener>();
        await _factory.WaitForUdpListenerAsync(TimeSpan.FromSeconds(5));

        using var client = new TestUdpClient();
        var reply = await client.SendAndReceiveAsync(
            new ServerRegistrationCommand
            {
                Token = "v1.test-token-abcdef",
                LocalIp = client.LocalEndpoint.Address.ToString(),
                LocalPort = client.LocalEndpoint.Port
            },
            listener.LocalEndpoint!,
            TimeSpan.FromSeconds(5));

        var result = reply.Should().BeOfType<PortCheckResultCommand>().Subject;
        result.State.Should().Be(PortCheckResult.Reachable);
        result.Message.Should().Contain("echo from M1");
    }

    [Fact]
    public async Task PortCheckRequestCommand_yields_Error_queued_reply()
    {
        var listener = _factory.Services.GetRequiredService<ApiUdpListener>();
        await _factory.WaitForUdpListenerAsync(TimeSpan.FromSeconds(5));

        using var client = new TestUdpClient();
        var reply = await client.SendAndReceiveAsync(
            new PortCheckRequestCommand { Port = 12345 },
            listener.LocalEndpoint!,
            TimeSpan.FromSeconds(5));

        var result = reply.Should().BeOfType<PortCheckResultCommand>().Subject;
        result.State.Should().Be(PortCheckResult.Error);
        result.Message.Should().Contain("probe queued");
    }
}