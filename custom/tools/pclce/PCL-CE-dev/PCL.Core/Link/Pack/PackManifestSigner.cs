using System;
using System.Security.Cryptography;

namespace PCL.Core.Link.Pack;

/// <summary>
/// 对内容清单做 ECDSA P-256 签名与校验。
/// </summary>
public static class PackManifestSigner
{
    public static string PublicKeyBase64(ECDsa key) =>
        Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    public static PackManifest Sign(PackManifest manifest, ECDsa key)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(key);

        var publicKey = PublicKeyBase64(key);
        var withKey = manifest with
        {
            Host = (manifest.Host ?? new PackHost()) with { PublicKey = publicKey }
        };

        var hash = PackManifestHasher.ComputeHash(withKey);
        var signature = key.SignHash(Convert.FromHexString(hash));

        return withKey with
        {
            PackHash = hash,
            Signature = Convert.ToBase64String(signature)
        };
    }

    public static bool VerifySignature(PackManifest manifest)
    {
        if (manifest is null ||
            string.IsNullOrWhiteSpace(manifest.PackHash) ||
            string.IsNullOrWhiteSpace(manifest.Signature) ||
            string.IsNullOrWhiteSpace(manifest.Host?.PublicKey))
            return false;

        try
        {
            if (!PackManifestVerifier.VerifyHash(manifest)) return false;

            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(
                Convert.FromBase64String(manifest.Host.PublicKey), out _);

            return key.VerifyHash(
                Convert.FromHexString(manifest.PackHash),
                Convert.FromBase64String(manifest.Signature));
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string Fingerprint(string? publicKeyBase64)
    {
        if (string.IsNullOrWhiteSpace(publicKeyBase64)) return "unsigned";
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
}
