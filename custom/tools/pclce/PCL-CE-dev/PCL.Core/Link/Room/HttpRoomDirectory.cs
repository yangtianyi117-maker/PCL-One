using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Link.Protocol;

namespace PCL.Core.Link.Room;

public sealed class HttpRoomDirectory(string baseUrl) : IRoomDirectory
{
    private readonly RoomIndexClient _client = new(baseUrl);

    public Task PublishAsync(RoomRecord record, CancellationToken ct = default) =>
        _client.PublishAsync(record, ct);

    public Task<IReadOnlyList<RoomRecord>> SearchAsync(RoomFilter filter, CancellationToken ct = default) =>
        _client.SearchAsync(filter, ct);

    public Task DeleteAsync(string roomId, ECDsa key, CancellationToken ct = default) =>
        _client.DeleteAsync(roomId, key, ct);

    public void Dispose() => _client.Dispose();
}
