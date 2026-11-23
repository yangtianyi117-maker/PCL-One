using System;
using System.IO;
using System.Security.Cryptography;
using PCL.Core.App;

namespace PCL.Core.Link.Pack;

/// <summary>
/// PCL 定制版 房主签名密钥。保存在便携数据目录下，首次使用时生成 ECDSA P-256。
/// </summary>
public static class PackHostKeyStore
{
    private static readonly object _Lock = new();
    private static ECDsa? _Key;

    public static string KeyPath => Path.Combine(Paths.Data, "HostKey", "host.pem");

    public static ECDsa GetOrCreate()
    {
        lock (_Lock)
        {
            if (_Key is not null) return _Key;

            var path = KeyPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (File.Exists(path))
            {
                var key = ECDsa.Create();
                key.ImportFromPem(File.ReadAllText(path));
                _Key = key;
                return key;
            }

            var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(path, created.ExportPkcs8PrivateKeyPem());
            _Key = created;
            return created;
        }
    }

    public static string PublicKeyBase64 => PackManifestSigner.PublicKeyBase64(GetOrCreate());
}
