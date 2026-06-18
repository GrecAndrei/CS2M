// SPDX-License-Identifier: MIT
// Pure encode/decode glue. The mod's serializer uses
// MessagePackSerializer.Serialize(command, options), so the server
// does the same. Each UDP datagram carries exactly one
// ApiCommandBase-derived command as a MessagePack payload.

using CS2M.ApiServer.Core.Commands;
using CS2M.ApiServer.Core.Dispatch;
using MessagePack;

namespace CS2M.ApiServer.Protocol.Codec;

public sealed class ApiCommandCodec : IApiCommandCodec
{
    private readonly MessagePackSerializerOptions _options;

    public ApiCommandCodec() : this(ApiMessagePackOptions.Default)
    {
    }

    public ApiCommandCodec(MessagePackSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public byte[] Encode(ApiCommandBase command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return MessagePackSerializer.Serialize(command, _options);
    }

    public bool TryDecode(ReadOnlySpan<byte> datagram, out ApiCommandBase? command)
    {
        command = null;
        if (datagram.IsEmpty)
        {
            return false;
        }

        // MessagePack 3.x only exposes Deserialize<T>(ReadOnlyMemory<byte>).
        // Copy the datagram into an array we own; the alternative
        // (ToArray() on the span) allocates too, so this is the same.
        var memory = new ReadOnlyMemory<byte>(datagram.ToArray());
        try
        {
            command = MessagePackSerializer.Deserialize<ApiCommandBase>(memory, _options);
            return command is not null;
        }
        catch (MessagePackSerializationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            // Thrown by the deserializer when the union discriminator
            // points to an unknown type or the body is malformed.
            return false;
        }
    }
}