using System.Security.Cryptography;
using System.Text;

namespace PCL.Link.Protocol;

/// <summary>
/// 房间记录的确定性编码与签名。索引服务端与客户端共用同一套规则。
/// </summary>
public static class RoomSignature
{
    private const string RecordDomain = "PCLPro.RoomRecord";
    private const string DeleteDomain = "PCLPro.RoomDelete";

    public static string PublicKeyBase64(ECDsa key) =>
        Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    public static string Sign(RoomRecord record, ECDsa key)
    {
        var hash = SHA256.HashData(ComputeCanonicalBytes(record));
        return Convert.ToBase64String(key.SignHash(hash));
    }

    public static bool Verify(RoomRecord record)
    {
        if (record is null || string.IsNullOrWhiteSpace(record.HostPublicKey) ||
            string.IsNullOrWhiteSpace(record.Signature))
            return false;

        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(record.HostPublicKey), out _);
            var hash = SHA256.HashData(ComputeCanonicalBytes(record));
            return key.VerifyHash(hash, Convert.FromBase64String(record.Signature));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string SignDelete(string roomId, long timestampSeconds, ECDsa key) =>
        Convert.ToBase64String(key.SignData(
            DeletePayload(roomId, timestampSeconds), HashAlgorithmName.SHA256));

    public static bool VerifyDelete(string roomId, long timestampSeconds, string publicKeyBase64, string signatureBase64)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestampSeconds) > 300) return false;

            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
            return key.VerifyData(
                DeletePayload(roomId, timestampSeconds),
                Convert.FromBase64String(signatureBase64),
                HashAlgorithmName.SHA256);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string Fingerprint(string publicKeyBase64)
    {
        try
        {
            var hash = SHA256.HashData(Convert.FromBase64String(publicKeyBase64));
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    public static byte[] ComputeCanonicalBytes(RoomRecord record)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            WriteString(writer, RecordDomain);
            writer.Write(record.Schema);
            WriteString(writer, record.RoomId);
            WriteString(writer, record.Name);
            WriteString(writer, record.MinecraftVersion);
            WriteString(writer, record.Loader);
            WriteString(writer, record.LoaderVersion);
            WriteString(writer, record.PackHash);
            writer.Write(record.Players);
            writer.Write(record.MaxPlayers);

            var tags = record.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToList();
            writer.Write(tags.Count);
            foreach (var tag in tags) WriteString(writer, tag);

            WriteString(writer, record.Visibility);
            WriteString(writer, record.JoinCode);
            WriteString(writer, record.HostName);
            WriteString(writer, record.HostPublicKey);
            writer.Write(record.UpdatedAt.ToUnixTimeSeconds());
            writer.Write(record.ExpiresAt.ToUnixTimeSeconds());
        }

        return stream.ToArray();
    }

    private static byte[] DeletePayload(string roomId, long timestampSeconds) =>
        Encoding.UTF8.GetBytes($"{DeleteDomain}|{roomId}|{timestampSeconds}");

    private static void WriteString(BinaryWriter writer, string? value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
