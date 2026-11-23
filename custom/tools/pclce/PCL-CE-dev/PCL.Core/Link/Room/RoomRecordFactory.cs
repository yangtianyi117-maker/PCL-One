using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Link.Pack;
using PCL.Link.Protocol;

namespace PCL.Core.Link.Room;

public static class RoomRecordFactory
{
    public static RoomRecord Create(
        string joinCode,
        PackManifest manifest,
        string hostName,
        int players,
        RoomPublishOptions options,
        ECDsa key)
    {
        var publicKey = PackManifestSigner.PublicKeyBase64(key);
        var roomId = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(publicKey + "|" + joinCode)))[..16].ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        var record = new RoomRecord
        {
            Schema = RoomRecord.CurrentSchema,
            RoomId = roomId,
            Name = string.IsNullOrWhiteSpace(options.Name) ? $"{hostName} 的房间" : options.Name.Trim(),
            MinecraftVersion = manifest.MinecraftVersion,
            Loader = manifest.Loader,
            LoaderVersion = manifest.LoaderVersion,
            PackHash = manifest.PackHash,
            Players = players,
            MaxPlayers = 8,
            Tags = options.Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList(),
            Visibility = "public",
            JoinCode = joinCode,
            HostName = hostName,
            HostPublicKey = publicKey,
            UpdatedAt = now,
            ExpiresAt = now.AddSeconds(90)
        };

        return record with { Signature = RoomSignature.Sign(record, key) };
    }

    public static RoomRecord Refresh(RoomRecord record, ECDsa key)
    {
        var now = DateTimeOffset.UtcNow;
        var refreshed = record with
        {
            UpdatedAt = now,
            ExpiresAt = now.AddSeconds(90),
            Signature = null
        };
        return refreshed with { Signature = RoomSignature.Sign(refreshed, key) };
    }
}
