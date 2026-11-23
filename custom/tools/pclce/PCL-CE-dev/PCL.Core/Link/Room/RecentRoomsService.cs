using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.App;

namespace PCL.Core.Link.Room;

/// <summary>一条最近加入过的房间。</summary>
public sealed record RecentRoomEntry(string Code, DateTimeOffset JoinedAt, string Source);

/// <summary>
/// 最近房间：加入成功后记一条，按房间码去重、最新在前、最多 20 条。
///
/// 第一性原理：「再进一次同一个房间」是最高频的重复动作，而它需要的全部信息
/// （房间码）在第一次加入时就已经知道了 —— 记下来即可，不该让用户再粘一次。
/// 只落一个 JSON 数组，与 PCL 的配置分离，坏了也不影响启动器。
/// </summary>
public static class RecentRoomsService
{
    private const int MaxEntries = 20;
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(Paths.SharedData, "link-history.json");

    public static string MigrationMarkerPath => Path.Combine(Paths.SharedData, "legacy-mclauncher-imported.json");

    public static IReadOnlyList<RecentRoomEntry> Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(FilePath)) return [];
                var array = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonArray;
                if (array is null) return [];

                return array.OfType<JsonObject>()
                    .Select(node => new RecentRoomEntry(
                        node["code"]?.GetValue<string>() ?? "",
                        DateTimeOffset.TryParse(node["at"]?.GetValue<string>(), out var at)
                            ? at
                            : DateTimeOffset.MinValue,
                        node["source"]?.GetValue<string>() ?? "room"))
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
                    .Take(MaxEntries)
                    .ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    /// <summary>记录一次加入；非 U/ 码（旧版 ML-/Terracotta）只作为历史展示，不写入。</summary>
    public static void Add(string? rawCode, string source = "room")
    {
        if (!LobbyCodeService.TryParse(rawCode, out var code)) return;

        lock (Gate)
        {
            try
            {
                var list = Load().ToList();
                list.RemoveAll(entry => string.Equals(entry.Code, code.FullCode, StringComparison.OrdinalIgnoreCase));
                list.Insert(0, new RecentRoomEntry(code.FullCode, DateTimeOffset.Now, source));
                if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);

                Directory.CreateDirectory(Paths.SharedData);
                var array = new JsonArray(list.Select(entry => (JsonNode)new JsonObject
                {
                    ["code"] = entry.Code,
                    ["at"] = entry.JoinedAt.ToString("O"),
                    ["source"] = entry.Source
                }).ToArray());
                File.WriteAllText(FilePath, array.ToJsonString(JsonOptions));
            }
            catch
            {
                // 历史记录失败不影响加入
            }
        }
    }

    public static int Clear()
    {
        lock (Gate)
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch
            {
                // ignored
            }

            return 0;
        }
    }

    /// <summary>
    /// 一次性导入旧 McLauncher 的 <c>.state/rooms.json</c>。
    /// 只导入能解析成 U/ 的码；ML- 旧码不会被伪造转换，避免加入一个不存在的网络。
    /// </summary>
    public static int ImportLegacyMcLauncher()
    {
        lock (Gate)
        {
            if (File.Exists(MigrationMarkerPath)) return 0;

            var imported = 0;
            var skipped = 0;
            foreach (var directory in LegacyStateDirectories())
            {
                var file = Path.Combine(directory, "rooms.json");
                if (!File.Exists(file)) continue;

                try
                {
                    var array = JsonNode.Parse(File.ReadAllText(file)) as JsonArray;
                    if (array is null) continue;

                    foreach (var node in array.OfType<JsonObject>())
                    {
                        var raw = node["code"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        if (LobbyCodeService.TryParse(raw, out var code))
                        {
                            var list = Load().ToList();
                            list.RemoveAll(entry =>
                                string.Equals(entry.Code, code.FullCode, StringComparison.OrdinalIgnoreCase));
                            list.Insert(0, new RecentRoomEntry(
                                code.FullCode,
                                DateTimeOffset.TryParse(node["at"]?.GetValue<string>(), out var at)
                                    ? at
                                    : DateTimeOffset.Now,
                                "legacy"));
                            if (list.Count > MaxEntries)
                                list.RemoveRange(MaxEntries, list.Count - MaxEntries);
                            Write(list);
                            imported++;
                        }
                        else
                        {
                            skipped++;
                        }
                    }
                }
                catch
                {
                    // 单个旧文件损坏不影响迁移
                }
            }

            try
            {
                Directory.CreateDirectory(Paths.SharedData);
                File.WriteAllText(MigrationMarkerPath, JsonSerializer.Serialize(new
                {
                    imported,
                    skipped,
                    at = DateTimeOffset.Now.ToString("O")
                }, JsonOptions));
            }
            catch
            {
                // ignored
            }

            return imported;
        }
    }

    private static void Write(List<RecentRoomEntry> list)
    {
        Directory.CreateDirectory(Paths.SharedData);
        var array = new JsonArray(list.Select(entry => (JsonNode)new JsonObject
        {
            ["code"] = entry.Code,
            ["at"] = entry.JoinedAt.ToString("O"),
            ["source"] = entry.Source
        }).ToArray());
        File.WriteAllText(FilePath, array.ToJsonString(JsonOptions));
    }

    private static IEnumerable<string> LegacyStateDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, ".state");

        // PCLProRuntime 的上两级通常是 McLauncher.exe 所在的目录（桌面）
        var runtimeDir = new DirectoryInfo(AppContext.BaseDirectory);
        if (runtimeDir.Parent?.Parent is { } launcherRoot)
            yield return Path.Combine(launcherRoot.FullName, ".state");

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop))
            yield return Path.Combine(desktop, ".state");

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(roaming))
            yield return Path.Combine(roaming, "McLauncher", ".state");
    }
}
