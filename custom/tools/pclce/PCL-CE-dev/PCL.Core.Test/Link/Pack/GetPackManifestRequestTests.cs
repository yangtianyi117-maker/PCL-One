using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Pack;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.Client.Requests;
using PCL.Core.Link.Scaffolding.Server;
using PCL.Core.Link.Scaffolding.Server.Abstractions;
using PCL.Core.Link.Scaffolding.Server.Handlers;

namespace PCL.Core.Test.Link.Pack;

[TestClass]
public sealed class GetPackManifestRequestTests
{
    [TestMethod]
    public void Request_SerializesPackHash_AndParsesManifest()
    {
        var manifest = CreateManifest();

        var writer = new ArrayBufferWriter<byte>();
        new GetPackManifestRequest(manifest.PackHash).WriteRequestBody(writer);
        Assert.AreEqual(manifest.PackHash, Encoding.UTF8.GetString(writer.WrittenSpan));

        var json = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var parsed = new GetPackManifestRequest().ParseResponseBody(json);

        Assert.IsNotNull(parsed);
        Assert.AreEqual(manifest.PackHash, parsed.PackHash);
        Assert.AreEqual(manifest.MinecraftVersion, parsed.MinecraftVersion);
        Assert.AreEqual(1, parsed.Mods.Count);
    }

    [TestMethod]
    public void Request_EmptyResponse_ReturnsNull()
    {
        var parsed = new GetPackManifestRequest().ParseResponseBody(ReadOnlyMemory<byte>.Empty);
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public async Task Handler_ReturnsManifest()
    {
        var manifest = CreateManifest();
        var handler = new GetPackManifestHandler();

        var (status, body) = await handler.HandleAsync(
            ReadOnlyMemory<byte>.Empty,
            new FakeServerContext(manifest),
            "session",
            default);

        Assert.AreEqual((byte)0, status);
        var parsed = JsonSerializer.Deserialize<PackManifest>(
            body.Span, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.IsNotNull(parsed);
        Assert.AreEqual(manifest.PackHash, parsed.PackHash);
    }

    [TestMethod]
    public async Task Handler_ReturnsError_WhenNoManifest()
    {
        var handler = new GetPackManifestHandler();

        var (status, body) = await handler.HandleAsync(
            ReadOnlyMemory<byte>.Empty,
            new FakeServerContext(null),
            "session",
            default);

        Assert.AreEqual((byte)32, status);
        Assert.IsTrue(body.IsEmpty);
    }

    private static PackManifest CreateManifest()
    {
        var manifest = new PackManifest
        {
            MinecraftVersion = "1.21.1",
            Loader = "neoforge",
            LoaderVersion = "21.1.80",
            Mods =
            [
                new PackMod
                {
                    Source = PackConstants.SourcePrivate,
                    Sha256 = "aaaa",
                    FileName = "foo.jar"
                }
            ]
        };

        return manifest with { PackHash = PackManifestHasher.ComputeHash(manifest) };
    }

    private sealed class FakeServerContext(PackManifest? manifest) : IServerContext
    {
        public IReadOnlyList<PlayerProfile> PlayerProfiles => [];
        public ConcurrentDictionary<string, TrackedPlayerProfile> TrackedPlayers { get; } = new();
        public void OnPlayerProfilesChanged() { }

        public event Action<IReadOnlyList<PlayerProfile>>? PlayerProfilesPing
        {
            add { }
            remove { }
        }

        public int MinecraftServerProt => 25565;
        public LobbyInfo UserLobbyInfo { get; } = new("code", "network", "secret");
        public string PlayerName => "host";
        public PackManifest? PackManifest => manifest;
    }
}
