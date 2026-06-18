using CS2M.ApiServer.Core.Commands;

namespace CS2M.ApiServer.Core.Dispatch;

public interface IApiCommandCodec
{
    byte[] Encode(ApiCommandBase command);
    bool TryDecode(ReadOnlySpan<byte> datagram, out ApiCommandBase? command);
}
