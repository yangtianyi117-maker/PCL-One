# 运维手册（RUNBOOK）

## 0. 前置检查（P0）

本机（Windows，构建/测试）：

```powershell
$PSVersionTable.PSVersion      # 5.1 可用
java -version                  # 需要 JDK 25 才能本地跑 Paper 26.3
Test-Path 'C:\Users\Administrator\Desktop\PCL Pro\PCLPro.exe'
```

服务器（Linux，SSH）：

```bash
uname -a
df -h /                 # 需要 >= 120 GB 空闲（1:1000）
free -h                 # 建议 >= 12 GB
java -version           # Paper 26.3 需要 Java 25
docker --version        # 可选；没有就用 systemd + paper.jar
ss -lunpt | grep -E '25565|19132|8100|25575'
```

预检脚本：

```powershell
.\server\scripts\preflight.ps1 -SshHost 124.221.231.213 -SshUser root
```

不满足磁盘/内存时：升配或换机；**换机必须保留旧 IP 的 `/pclpro/` HTTP 端点**，因为桌面「联机」页的 `servers.json` URL 是硬编码的。

## 1. 首次部署

1. 停旧服，备份旧世界与 `servers.json`：
   ```bash
   cd /srv/pcl-earth
   tar czf backups/pre-earth-$(date +%F).tar.gz world servers.json
   ```
2. 上传地图（断点续传）：
   ```powershell
   rsync -avP --partial .\Earth1000\ root@124.221.231.213:/srv/pcl-earth/
   ```
3. 上传服务端配置与插件：
   ```powershell
   .\server\scripts\deploy.ps1 -SshHost 124.221.231.213 -SshUser root -UploadConfig
   ```
4. 启动：
   ```bash
   cd /srv/pcl-earth && docker compose up -d
   docker compose logs -f --tail=100
   ```
5. 首次启动后设置出生点/边界（见 `docs/COORDS.md`），把 `gamemode=survival`、`force-gamemode=true` 写进 `server.properties` 后重启一次。
6. 更新控制面：
   ```bash
   cp control/servers.json /var/www/pclpro/servers.json
   cp control/earth.html  /var/www/pclpro/earth.html
   cp control/icon-earth.png /var/www/pclpro/icon-earth.png
   ```
7. 冒烟：
   ```powershell
   .\server\scripts\smoke.ps1 -HostName 124.221.231.213
   ```

## 2. 备份

每日 04:00（服务器 cron）：

```bash
#!/usr/bin/env bash
set -euo pipefail
cd /srv/pcl-earth
ts=$(date +%F-%H%M)
docker compose exec -T mc rcon-cli save-off
docker compose exec -T mc rcon-cli save-all
tar czf "backups/world-$ts.tar.gz" data/Earth1000/level.dat data/Earth1000/region data/Earth1000/datapacks
docker compose exec -T mc rcon-cli save-on
find backups -name 'world-*.tar.gz' -mtime +7 -delete
```

- 保留 7 日 + 4 周（周备份单独目录，不参与 7 日清理）。
- 备份文件不得放在可公开下载的目录。
- 每月做一次恢复演练（staging 目录，不碰线上）。

恢复：

```bash
cd /srv/pcl-earth
docker compose stop mc
mv data/Earth1000 data/Earth1000.broken-$(date +%F-%H%M)
tar xzf backups/world-<ts>.tar.gz -C data
docker compose start mc
```

## 3. 升级

1. 先备份。
2. 在 staging 副本上启动新版本，进 3 个区块点。
3. 更新 `server/plugins.lock.json` 的版本与 SHA256。
4. 低峰停服 → 替换 jar/插件 → 启动 → 冒烟。
5. 失败回滚：恢复旧 jar/插件与备份世界，30 分钟内恢复。

## 4. 监控（最小集）

- TPS：`/tps` 或 spark；低于 19.5 告警。
- 磁盘：`df -h`，低于 20 GB 告警（区块会持续增长）。
- 备份：每日检查 `backups/` 是否有当天文件。
- 玩家：`/list`；Bedrock 与 Java 人数分别统计。
- 崩溃：`data/logs/latest.log` + `crash-reports/`。

## 5. 回滚清单

部署脚本会保留：

- `backups/pre-earth-<date>/world`（旧世界）
- `backups/pre-earth-<date>/servers.json`
- 旧 APK（`dist/PCLProMobile-1.1.0.apk`）
- 客户端安装器的原始备份（`<GameDir>\pcl-earth\backup-<ts>`）

任何一步失败：停服 → 恢复世界/JSON → 启服 → 客户端卸载脚本还原。
