PCL.RoomIndex
=============

PCL 定制版 公开房索引参考实现（ASP.NET Core Minimal API + SQLite）。

运行
----

```powershell
dotnet run --project PCL.RoomIndex --urls http://127.0.0.1:5180
```

默认数据库在程序目录 `rooms.db`，可用配置覆盖：

```powershell
$env:RoomIndex__DatabasePath = "D:\pcl\rooms.db"
dotnet run --project PCL.RoomIndex --urls http://0.0.0.0:5180
```

接口
----

- `PUT /v1/rooms/{roomId}`：房主发布/心跳，要求有效 ECDSA 签名。
- `GET /v1/rooms?mc=&loader=&q=&limit=`：搜索未过期公开房。
- `GET /v1/rooms/{roomId}`：单条。
- `DELETE /v1/rooms/{roomId}?ts=&key=&sig=`：房主删除，签名 5 分钟内有效。
- `POST /v1/rooms/{roomId}/report`：举报计数。
- `GET /health`：健康检查。

客户端配置
----------

默认情况下，客户端不配置地址，直接使用 `ntfy.sh` 公共 topic `pclpro-rooms-v1` 做零配置公开房发现；房主和加入者都不需要输入任何地址。

如果要自建索引，在 PCL 定制版 中设置 `LinkRoomIndexUrl`，或启动前设置环境变量：

```powershell
$env:PCL_PRO_ROOM_INDEX_URL = "http://127.0.0.1:5180"
```

自建地址优先于 ntfy。关闭零配置频道：

```text
LinkNtfyPublicRooms = false
```

公开房记录本身带 ECDSA 签名，客户端只接受验签通过且未过期的记录；但公共频道是公开的，不做账号/审核，房间口令也是公开的。
