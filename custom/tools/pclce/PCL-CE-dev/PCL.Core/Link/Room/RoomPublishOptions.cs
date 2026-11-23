using System.Collections.Generic;

namespace PCL.Core.Link.Room;

/// <summary>
/// 房主发布房间时的选项。
/// </summary>
public sealed record RoomPublishOptions(
    bool IsPublic,
    string Name,
    IReadOnlyList<string> Tags);
