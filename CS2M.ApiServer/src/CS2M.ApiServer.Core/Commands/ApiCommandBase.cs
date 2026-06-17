// SPDX-License-Identifier: MIT
// This is a protocol mirror; the canonical definitions live in the mod repo
// (CS2M/Commands/ApiServer/ApiCommandBase.cs). Keep them in sync manually or
// generate from a shared schema.
//
// The server intentionally depends on no game code. The wire format is
// pure MessagePack, not LiteNetLib-framed.
//
// Polymorphism note: MessagePack 3.x uses DynamicUnionResolver. The
// concrete subtypes are registered at runtime by
// CS2M.ApiServer.Protocol.Codec.ApiMessagePackOptionsFactory. Subtype
// keys are assigned in the registration order, so the factory below
// mirrors the order the mod's ApiCommand.cs:RefreshModel() uses when
// it iterates ApiCommandHandler subclasses.

namespace CS2M.ApiServer.Core.Commands;

/// <summary>
///     Abstract base class for every API command that travels on UDP/4242
///     between a CS2M game server and the CS2M.ApiServer backend.
/// </summary>
public abstract class ApiCommandBase
{
}
