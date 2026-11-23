# 零整合包联机（PCL 定制版 v0.1）

房主把 mod 放进实例后开房，客户端按房间清单自动把同一个 `R` 物化成受管实例，再通过现有 EasyTier/Scaffolding 联机。加入者不需要收整合包，也不需要自己装加载器。

## 组成

| 文件 | 作用 |
|---|---|
| `PackManifest.cs` | 清单数据模型 |
| `PackManifestHasher.cs` | 确定性哈希与校验 |
| `PackManifestBuilder.cs` | 扫描实例的 `mods/`、`config/`，生成清单 |
| `PackSourceResolver.cs` | 把本地 mod 解析成公开源/私有源 |
| `ModrinthPackSourceResolver.cs` | 按 SHA-512 反查 Modrinth 并取文件 URL |
| `PackProvisioner.cs` | 按清单安装 MC/加载器、下 mod、写配置、写标记 |
| `PackFeature.cs` | 功能开关 |

Scaffolding 侧新增：

- 客户端 `GetPackManifestRequest`（`c:pack_manifest`）
- 服务端 `GetPackManifestHandler`
- `ScaffoldingServerContext.PackManifest`

## 启用

PCL 定制版 构建默认开启；临时关闭用：

```powershell
$env:PCL_PRO_PACK_SYNC = "0"
```

开启后：

- 房主在联机页创建大厅时，会从当前选中实例生成清单并随房间发布；
- 加入者进入房间后自动拉取清单、准备实例并启动游戏。

## 清单示例

```json
{
  "schema": 1,
  "packHash": "…",
  "mc": "1.21.1",
  "loader": "neoforge",
  "loaderVersion": "21.1.80",
  "mods": [
    {
      "source": "modrinth",
      "projectId": "…",
      "versionId": "…",
      "sha256": "…",
      "fileName": "foo.jar",
      "side": "both",
      "blob": "https://cdn.modrinth.com/…"
    }
  ],
  "configs": [
    { "path": "config/foo.toml", "sha256": "…" }
  ]
}
```

`packHash` 对规范化的清单内容做 SHA-256；`PackHash` 与 `signature` 不参与计算。

## 地区限制

PCL 定制版 仍保留 PCL 的地区限制。大陆环境（中国时区 + CN 地区）下，实例缺客户端文件时启动会抛 `CancelledException`，零整合包也无法自动安装 MC/加载器。

临时解除（仅测试/自用，风险自负）：

```powershell
$env:PCL_PRO_DISABLE_REGION_RESTRICTION = "1"
```

或在 PCL 定制版 设置配置文件里把 `ProDisableRegionRestriction` 设为 `true`。

## v0.2 进展

- 清单可选 ECDSA P-256 签名；加入者会校验签名，篡改的清单会被拒绝。
- 房主密钥保存在便携数据目录 `PCL/HostKey/host.pem`。
- 加入者通过 `PCL/PackCache` 按 SHA-256 缓存 mod，跨实例复用；优先硬链接，失败回退复制。
- 纯原版 / 0 mod 房间也会自动安装对应 MC 版本（不再要求 `HasContent`）。
- 公开房索引见 `PCL.RoomIndex`；客户端通过 `LinkRoomIndexUrl` 或
  `PCL_PRO_ROOM_INDEX_URL` 配置自建索引。
- 未配置自建索引时，默认使用 `ntfy.sh` 公共 topic `pclpro-rooms-v1` 做零配置公开房发现；
  可用 `LinkNtfyPublicRooms=false` 关闭。公共频道是公开的，记录带签名但不做账号/审核。

## 当前边界（v0.1）

- 公开 mod 通过 Modrinth 解析；CurseForge、私有 blob 尚未接入。
- 私有 mod 没有 `blob` 时，加入者会收到明确失败，而不是静默跳过。
- 配置只记录哈希，`blob` 为空时不会写入客户端。
- 安装管线复用 `DownloadUi.McInstallAsync`；只支持 vanilla / Forge / NeoForge / Fabric。
- 在线模式、Quilt/LegacyFabric、跨版本协议桥不在本阶段。

## 后续

1. 私有 mod 的房主 blob 端点（`c:pack_blob` 或本地 HTTP）。
2. CurseForge fingerprint 解析。
3. 公开房间索引与口令搜索。
4. `PCL.Protocol` 全版本协议桥（vanilla 跨版本）。
