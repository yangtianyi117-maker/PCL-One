using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Link.Protocol;

namespace PCL.Core.Link.Room;

/// <summary>
/// 公开房目录。可以是自建 HTTP 索引，也可以是零配置的公共频道。
/// </summary>
public interface IRoomDirectory : System.IDisposable
{
    Task PublishAsync(RoomRecord record, CancellationToken ct = default);

    Task<IReadOnlyList<RoomRecord>> SearchAsync(RoomFilter filter, CancellationToken ct = default);

    Task DeleteAsync(string roomId, ECDsa key, CancellationToken ct = default);
}
