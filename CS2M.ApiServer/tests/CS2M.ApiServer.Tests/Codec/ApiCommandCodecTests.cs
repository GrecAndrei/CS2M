// SPDX-License-Identifier: MIT
// Codec-level round-trip tests. Asserts that every concrete command
// type survives an encode/decode cycle through the wire-format
// options the API server uses.

using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Protocol.Codec;
using FluentAssertions;
using Xunit;

namespace CS2M.ApiServer.Tests.Codec;

public class ApiCommandCodecTests
{
    private readonly ApiCommandCodec _codec = new();

    [Fact]
    public void ServerRegistrationCommand_round_trips()
    {
        var original = new ServerRegistrationCommand
        {
            Token = "v1.test-token-abcdef",
            LocalIp = "192.0.2.10",
            LocalPort = 4242
        };
        var bytes = _codec.Encode(original);
        _codec.TryDecode(bytes, out var decoded).Should().BeTrue();
        decoded.Should().BeOfType<ServerRegistrationCommand>();
        var actual = (ServerRegistrationCommand)decoded!;
        actual.Token.Should().Be(original.Token);
        actual.LocalIp.Should().Be(original.LocalIp);
        actual.LocalPort.Should().Be(original.LocalPort);
    }

    [Fact]
    public void PortCheckRequestCommand_round_trips()
    {
        var original = new PortCheckRequestCommand { Port = 39847 };
        var bytes = _codec.Encode(original);
        _codec.TryDecode(bytes, out var decoded).Should().BeTrue();
        decoded.Should().BeOfType<PortCheckRequestCommand>();
        ((PortCheckRequestCommand)decoded!).Port.Should().Be(original.Port);
    }

    [Fact]
    public void PortCheckResultCommand_round_trips()
    {
        foreach (var state in new[] { PortCheckResult.Reachable, PortCheckResult.Unreachable, PortCheckResult.Error })
        {
            var original = new PortCheckResultCommand
            {
                State = state,
                Message = $"state={state}"
            };
            var bytes = _codec.Encode(original);
            _codec.TryDecode(bytes, out var decoded).Should().BeTrue();
            decoded.Should().BeOfType<PortCheckResultCommand>();
            var actual = (PortCheckResultCommand)decoded!;
            actual.State.Should().Be(state);
            actual.Message.Should().Be(original.Message);
        }
    }

    [Fact]
    public void TryDecode_returns_false_for_truncated_payload()
    {
        var bytes = _codec.Encode(new PortCheckRequestCommand { Port = 1000 });
        // chop the last byte
        var truncated = bytes.AsSpan(0, bytes.Length - 1).ToArray();
        _codec.TryDecode(truncated, out var decoded).Should().BeFalse();
        decoded.Should().BeNull();
    }

    [Fact]
    public void TryDecode_returns_false_for_empty_payload()
    {
        _codec.TryDecode(ReadOnlySpan<byte>.Empty, out var decoded).Should().BeFalse();
        decoded.Should().BeNull();
    }

    [Fact]
    public void TryDecode_returns_false_for_random_garbage()
    {
        var garbage = new byte[] { 0xc1, 0xff, 0xee, 0xaa };
        _codec.TryDecode(garbage, out var decoded).Should().BeFalse();
        decoded.Should().BeNull();
    }
}