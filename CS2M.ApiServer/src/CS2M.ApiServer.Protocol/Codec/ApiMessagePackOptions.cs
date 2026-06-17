// SPDX-License-Identifier: MIT
// Builds the MessagePack serializer options that match the wire format
// the mod uses (CS2M/Commands/ApiServer/ApiCommand.cs:RefreshModel).
//
// The mod's options are produced by:
//
//     IFormatterResolver resolver = CompositeResolver.Create(
//         MessagePack.Unity.Extension.UnityBlitResolver.Instance,
//         MessagePack.Unity.UnityResolver.Instance,
//         StandardResolver.Instance
//     );
//     var options = MessagePackSerializerOptions.Standard
//         .WithResolver(resolver)
//         .Configure();   // <- Attributeless extension method
//     foreach (var handler in handlers)
//         options.SubType(typeof(ApiCommandBase), handler.GetDataType());
//     _model = options.Build();
//
// `Configure()` is an extension method from MessagePack.Attributeless
// that returns a `MessagePackSerializerOptionsBuilder`. Calling
// `SubType(base, sub)` on the builder registers a
// `SubTypeFormatter<TBase>` which, by default, also auto-keys the
// subtype (Attributeless's `Configuration` constructor sets
// `_doImplicitlyAutokeySubtypes = true`).
//
// Why we use Attributeless on the server too
// -----------------------------------------
// The mod's `StandardResolver.Instance` chain cannot serialize plain
// POCOs without `[Key]` attributes — it would throw at runtime. The
// reason it works in the mod is that `Configure()` from
// Attributeless installs a `ConfigurableKeyFormatter<T>` for each
// concrete command type, which serializes properties in a stable
// auto-assigned key order. To produce wire bytes the mod can read,
// the server must run the same code path.
//
// Wire layout for ApiCommandBase-derived commands
// -----------------------------------------------
//   - Int32 subtype key (assigned by SubTypeFormatter in registration order)
//   - MessagePack array of property values in Attributeless's auto-key order
//
// The Unity resolvers in the mod's chain (UnityBlitResolver,
// UnityResolver) only handle `UnityEngine.*` types; our server has no
// Unity types in scope, so omitting them from the resolver chain does
// not change the bytes for our wire types.

using CS2M.ApiServer.Core.Commands;
using MessagePack;
using MessagePack.Attributeless;
using MessagePack.Resolvers;

namespace CS2M.ApiServer.Protocol.Codec;

public static class ApiMessagePackOptions
{
    private static readonly Lazy<MessagePackSerializerOptions> _lazy = new(Build);

    /// <summary>
    ///     The default options used by the API server. This is a singleton;
    ///     share it across the whole process for best performance.
    /// </summary>
    public static MessagePackSerializerOptions Default => _lazy.Value;

    private static MessagePackSerializerOptions Build()
    {
        IFormatterResolver resolver = CompositeResolver.Create(
            // The mod also registers Unity resolvers first; we have no
            // Unity types in scope, so the chain is effectively the
            // standard resolver for the mod's POCO wire types.
            StandardResolver.Instance
        );

        return MessagePackSerializerOptions.Standard
            .WithResolver(resolver)
            .WithSecurity(MessagePackSecurity.UntrustedData)
            .Configure()
            // Order matters: must match the order the mod's
            // ApiCommand.cs:RefreshModel() invokes SubType. The mod
            // iterates ApiCommandHandler subclasses via
            // AssemblyHelper.FindClassesInCSM which uses
            // Assembly.GetTypes(). On .NET, that order is the assembly
            // metadata order, which Roslyn emits alphabetically within
            // a single namespace. Today the three handlers are:
            //
            //   PortCheckRequestHandler    -> PortCheckRequestCommand   (key 0)
            //   PortCheckResultHandler     -> PortCheckResultCommand    (key 1)
            //   ServerRegistrationHandler  -> ServerRegistrationCommand (key 2)
            //
            // If the mod adds a new handler, this list must be updated
            // to insert it in the matching metadata-order position.
            .SubType(typeof(ApiCommandBase), typeof(PortCheckRequestCommand))
            .SubType(typeof(ApiCommandBase), typeof(PortCheckResultCommand))
            .SubType(typeof(ApiCommandBase), typeof(ServerRegistrationCommand))
            .Build();
    }
}