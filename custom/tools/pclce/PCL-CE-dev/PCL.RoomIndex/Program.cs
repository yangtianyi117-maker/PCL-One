using System.Threading.RateLimiting;
using PCL.Link.Protocol;
using PCL.RoomIndex;

var builder = WebApplication.CreateBuilder(args);

var databasePath = builder.Configuration["RoomIndex:DatabasePath"]
                   ?? Path.Combine(AppContext.BaseDirectory, "rooms.db");
builder.Services.AddSingleton(new RoomStore(databasePath));
builder.Services.AddHostedService<RoomCleanupService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("rooms", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();
app.UseRateLimiter();

var store = app.Services.GetRequiredService<RoomStore>();
store.Initialize();

app.MapGet("/health", () => Results.Ok(new { ok = true, service = "pcl-room-index" }));

app.MapGet("/v1/rooms", (string? mc, string? loader, string? q, int? limit, RoomStore roomStore) =>
{
    var records = roomStore.Query(new RoomFilter
    {
        MinecraftVersion = mc,
        Loader = loader,
        Query = q,
        Limit = limit ?? 50
    });
    return Results.Ok(records);
}).RequireRateLimiting("rooms");

app.MapGet("/v1/rooms/{roomId}", (string roomId, RoomStore roomStore) =>
{
    var record = roomStore.Get(roomId);
    return record is null ? Results.NotFound() : Results.Ok(record);
}).RequireRateLimiting("rooms");

app.MapPut("/v1/rooms/{roomId}", (string roomId, RoomRecord record, RoomStore roomStore) =>
{
    if (string.IsNullOrWhiteSpace(roomId) || !string.Equals(roomId, record.RoomId, StringComparison.Ordinal))
        return Results.BadRequest(new { error = "roomId 不一致" });

    if (!record.IsPublic)
        return Results.BadRequest(new { error = "只接受公开房" });

    if (record.IsExpired)
        return Results.BadRequest(new { error = "记录已过期" });

    if (string.IsNullOrWhiteSpace(record.Name) || record.Name.Length > 64 ||
        string.IsNullOrWhiteSpace(record.JoinCode) || record.JoinCode.Length > 128 ||
        string.IsNullOrWhiteSpace(record.MinecraftVersion) || record.MinecraftVersion.Length > 64 ||
        string.IsNullOrWhiteSpace(record.HostPublicKey))
        return Results.BadRequest(new { error = "字段无效" });

    if (!RoomSignature.Verify(record))
        return Results.BadRequest(new { error = "签名无效" });

    roomStore.Upsert(record);
    return Results.Ok(new { ok = true });
}).RequireRateLimiting("rooms");

app.MapDelete("/v1/rooms/{roomId}", (
    string roomId, long ts, string key, string sig, RoomStore roomStore) =>
{
    if (!RoomSignature.VerifyDelete(roomId, ts, key, sig))
        return Results.BadRequest(new { error = "签名无效" });

    return roomStore.Remove(roomId) ? Results.Ok(new { ok = true }) : Results.NotFound();
}).RequireRateLimiting("rooms");

app.MapPost("/v1/rooms/{roomId}/report", (string roomId, ReportRequest request, RoomStore roomStore) =>
{
    roomStore.Report(roomId);
    return Results.Ok(new { ok = true, reason = request.Reason });
}).RequireRateLimiting("rooms");

app.Run();

public sealed record ReportRequest(string? Reason);

public partial class Program;
