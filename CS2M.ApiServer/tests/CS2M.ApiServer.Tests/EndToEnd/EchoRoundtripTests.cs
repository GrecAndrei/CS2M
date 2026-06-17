// SPDX-License-Identifier: MIT
// End-to-end tests: spin up the API server, send a
// ServerRegistrationCommand or PortCheckRequestCommand from a test
// UDP client, assert the matching PortCheckResultCommand comes back.

using System.Net;
using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Udp;
using CS2M.ApiServer.Storage.Repositories;
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
    public async Task ServerRegistrationCommand_yields_Reachable_reply_and_upserts_row()
    {
        var listener = _factory.Services.GetRequiredService<ApiUdpListener>();
        await _factory.WaitForUdpListenerAsync(TimeSpan.FromSeconds(5));

        using var client = new TestUdpClient();
        var token = "v1.reg." + Guid.NewGuid().ToString("N");
        var reply = await client.SendAndReceiveAsync(
            new ServerRegistrationCommand
            {
                Token = token,
                LocalIp = client.LocalEndpoint.Address.ToString(),
                LocalPort = client.LocalEndpoint.Port
            },
            listener.LocalEndpoint!,
            TimeSpan.FromSeconds(5));

        var result = reply.Should().BeOfType<PortCheckResultCommand>().Subject;
        result.State.Should().Be(PortCheckResult.Reachable);
        result.Message.Should().Contain("registration accepted");

        // And the row is visible to the REST API.
        var http = _factory.CreateClient();
        var response = await http.GetAsync($"/api/v1/servers/{token}");
        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(token);
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

    [Fact]
    public async Task ServerRegistrationCommand_invalid_token_rejected()
    {
        var listener = _factory.Services.GetRequiredService<ApiUdpListener>();
        await _factory.WaitForUdpListenerAsync(TimeSpan.FromSeconds(5));

        using var client = new TestUdpClient();
        var reply = await client.SendAndReceiveAsync(
            new ServerRegistrationCommand
            {
                Token = "bad token with spaces!",
                LocalIp = "127.0.0.1",
                LocalPort = 4242
            },
            listener.LocalEndpoint!,
            TimeSpan.FromSeconds(5));

        var result = reply.Should().BeOfType<PortCheckResultCommand>().Subject;
        result.State.Should().Be(PortCheckResult.Error);
        result.Message.Should().Contain("invalid token");
    }
}