# PCL Pro 联机协议

## 1. 房间码

- 唯一格式：`U/XXXX-XXXX-XXXX-XXXX`（Scaffolding `LobbyCodeGenerator`）。
- 网络名/网络密钥从码派生，不再单独传递。
- 归一化：`LobbyCodeService.Normalize` 支持小写、空格、无连字符。
- 旧格式：`ML-`（McLauncher）、旧 PCL CE base32、Terracotta `U/`（25 位）只读识别，
  不能用于当前 Scaffolding 入房；`LobbyCodeService.IsLegacyCode` 用于提示/迁移。
- 端口不写进码：由 Scaffolding `c:server_port` 返回；局域网由应答器返回。

## 2. 局域网（免房间码）

房主应答（UDP，默认 25566）：

```
问: MCROOM1?
答: MCROOM1 <port> <0|1> <name>
```

加入方发现：

1. 向本机各网段定向广播 + `255.255.255.255:25566` 发 `MCROOM1?`；
2. 收 40% 超时窗口；
3. 无应答时退化为逐地址扫描（/22–/30，最多 512 个/网段），再收 60% 窗口；
4. 结果按 `host:port` 去重，排除回环。

房主端口来源：`LanWorldWatcher` 读实例 `logs/latest.log`：

- `Published LAN server on port N`（26.x）
- `Started serving on N`（旧版）
- `Unpublishing integrated server` → 收回应答器

加入方启动参数：`--quickPlayMultiplayer "host:port"`（PCL 既有逻辑），**不启动 EasyTier**。

## 3. 手机（Bedrock）

- Geyser Standalone 下载：`https://download.geysermc.org/v2/projects/geyser/versions/latest/builds/latest/downloads/standalone`
- 缓存：`PCL/Geyser/Geyser-Standalone.jar`，配置同目录 `config.yml`
- 入口：`0.0.0.0:<LinkPhonePort>`，默认 19132；`transport: both`
- 后端：`127.0.0.1:<房主 MC 端口>`，`auth-type: offline`
- 校验：`RakNetProbe` 未连接 Ping，回包以 `0x1C` 开头。

二维码/复制载荷：

```
pclpro://phone?c=<U/码>&n=<网络名>&s=<网络密钥>&h=<房主虚拟IP>&p=19132[&r=<中继>]
```

## 4. 深链

```
pclpro://join?c=<U/码>
```

- 注册：`HKCU\Software\Classes\pclpro\shell\open\command` →
  `"<exe>" join-code --url "%1"`
- 首实例：`StartupService` 解析 `join-code`，`ProDeepLink` 把码放进联机页输入框。
- 第二实例：`SingleInstanceService` 通过 RPC 转发 `join-code`。
- bootstrapper 必须把 `argv[1..]` 透传给真正的 PCL 进程（已修）。

## 5. 公开房

- 记录：`PCL.Link.Protocol.RoomRecord`，ECDSA P-256 签名，90 秒过期。
- 目录：`LinkRoomIndexUrl` 自建优先；否则 ntfy `pclpro-rooms-v1`（可关）。
- 删除 McLauncher 的固定槽位房：先到先得、无仲裁，已被目录取代。

## 6. 兼容边界

- 同版本/同加载器是准确联机的前提；跨版本仅 vanilla 走 ViaProxy。
- 26.3 无映射，游戏内桥 mod 仅 1.21.1；26.3 在“工具 → 联机”页建房。
- 旧 `ML-` 房间码不能加入 `U/` 房间，迁移时只作为历史展示。
