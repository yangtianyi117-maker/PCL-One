using PCL.Core.App;

namespace PCL.Core.Link.Room;

public static class RoomDirectoryFactory
{
    public static bool IsAvailable =>
        Config.Link.PublicRoomsEnabled &&
        (RoomIndexClient.IsConfigured || Config.Link.NtfyPublicRooms);

    public static IRoomDirectory? TryCreate()
    {
        if (!Config.Link.PublicRoomsEnabled) return null;
        if (RoomIndexClient.IsConfigured)
            return new HttpRoomDirectory(RoomIndexClient.ConfiguredUrl);
        return Config.Link.NtfyPublicRooms ? new NtfyRoomDirectory() : null;
    }
}
