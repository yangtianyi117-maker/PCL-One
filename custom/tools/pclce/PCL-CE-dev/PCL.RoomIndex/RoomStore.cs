using System.Text.Json;
using Microsoft.Data.Sqlite;
using PCL.Link.Protocol;

namespace PCL.RoomIndex;

/// <summary>
/// SQLite 房间存储。只保存公开房，过期记录由后台服务清理。
/// </summary>
public sealed class RoomStore(string databasePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath
    }.ToString();

    public void Initialize()
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS rooms (
                room_id TEXT PRIMARY KEY,
                payload TEXT NOT NULL,
                updated_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                reports INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS ix_rooms_expires ON rooms(expires_at);
            """;
        command.ExecuteNonQuery();
    }

    public void Upsert(RoomRecord record)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO rooms(room_id, payload, updated_at, expires_at, reports)
            VALUES ($id, $payload, $updated, $expires, 0)
            ON CONFLICT(room_id) DO UPDATE SET
                payload = excluded.payload,
                updated_at = excluded.updated_at,
                expires_at = excluded.expires_at;
            """;
        command.Parameters.AddWithValue("$id", record.RoomId);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(record, JsonOptions));
        command.Parameters.AddWithValue("$updated", record.UpdatedAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$expires", record.ExpiresAt.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<RoomRecord> Query(RoomFilter filter)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        var where = new List<string> { "expires_at > $now" };
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if (!string.IsNullOrWhiteSpace(filter.MinecraftVersion))
        {
            where.Add("json_extract(payload, '$.mc') = $mc");
            command.Parameters.AddWithValue("$mc", filter.MinecraftVersion);
        }

        if (!string.IsNullOrWhiteSpace(filter.Loader))
        {
            where.Add("json_extract(payload, '$.loader') = $loader");
            command.Parameters.AddWithValue("$loader", filter.Loader);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            where.Add("(json_extract(payload, '$.name') LIKE $q OR json_extract(payload, '$.hostName') LIKE $q)");
            command.Parameters.AddWithValue("$q", "%" + filter.Query.Trim() + "%");
        }

        command.CommandText =
            $"SELECT payload FROM rooms WHERE {string.Join(" AND ", where)} " +
            "ORDER BY json_extract(payload, '$.players') DESC, updated_at DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(filter.Limit, 1, 100));

        var result = new List<RoomRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var record = JsonSerializer.Deserialize<RoomRecord>(reader.GetString(0), JsonOptions);
                if (record is not null && !record.IsExpired) result.Add(record);
            }
            catch (JsonException)
            {
                // 跳过损坏记录
            }
        }

        return result;
    }

    public RoomRecord? Get(string roomId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM rooms WHERE room_id = $id AND expires_at > $now LIMIT 1;";
        command.Parameters.AddWithValue("$id", roomId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var payload = command.ExecuteScalar() as string;
        return payload is null ? null : JsonSerializer.Deserialize<RoomRecord>(payload, JsonOptions);
    }

    public bool Remove(string roomId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rooms WHERE room_id = $id;";
        command.Parameters.AddWithValue("$id", roomId);
        return command.ExecuteNonQuery() > 0;
    }

    public void Report(string roomId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE rooms SET reports = reports + 1 WHERE room_id = $id;";
        command.Parameters.AddWithValue("$id", roomId);
        command.ExecuteNonQuery();
    }

    public int Cleanup()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM rooms WHERE expires_at <= $now;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
