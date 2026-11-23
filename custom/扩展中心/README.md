# PCL Pro 桌面端 · 旁路扩展中心

PCL Pro 本体是编译后的 .NET/WPF 程序（`PCLPro.exe` + `PCLProRuntime`），没有源码，改不了它的 UI。
但它启动的是标准 Minecraft 目录，所以桌面端可以在**不碰 PCL Pro 进程**的前提下做两件事：

1. **客户端 Mod**：从 Modrinth 解析当前实例的构建（含 required 前置），下载到 `<游戏目录>/mods`；
2. **世界服务器**：把 `pcl_servers.json` 里的 Java 服务器合并进 `<游戏目录>/servers.dat`，并自动备份。

第一性：客户端 Mod 只改本地客户端，服务端不需要安装；服务器列表是纯数据文件。两者都不跨协议桥。

## 文件

```text
扩展中心/
├─ pcl_extend.py          # 全部逻辑，Python 3 标准库，无第三方依赖
├─ pcl_client_mods.json   # 与手机版同一份 48 个客户端 Mod 目录
├─ pcl_servers.json       # 与手机版同一份公共服务器列表
└─ README.md
```

## 用法

先看当前实例能不能识别（从 `<游戏目录>/PCL.ini` 的 `Version:` 行读取，例如 `26.3-Fabric`）：

```powershell
python pcl_extend.py mods --list
```

全网搜索（按当前实例的 MC 版本 / 加载器过滤）：

```powershell
python pcl_extend.py mods --search sodium
# 然后用 --install <slug> --apply 安装任意搜索结果
```

安装 Mod（默认**只预览**，加 `--apply` 才写入）：

```powershell
# 预览
python pcl_extend.py mods --install sodium lithium

# 真正安装到 <游戏目录>/mods（含 required 前置）
python pcl_extend.py mods --install sodium lithium --apply

# 安装精选目录里的全部（慎用，会装很多）
python pcl_extend.py mods --install --all --apply

# 装到别的目录（比如先验证）
python pcl_extend.py mods --install ferrite-core --apply --dest .\staging
```

查看已安装 / 检查更新 / 更新：

```powershell
# 列出 <game>/mods 里每个 jar 的版本（能识别 Modrinth 来源的）
python pcl_extend.py mods --installed

# 只列可更新的
python pcl_extend.py mods --updates

# 更新全部可更新项（下载新文件并删除旧文件）
python pcl_extend.py mods --update

# 只更新指定 jar
python pcl_extend.py mods --update sodium-fabric-0.9.3+mc26.3.jar
```

`--dest` 同样适用于这三个命令，例如先对 staging 目录演练。

服务器合并（默认只预览）：

```powershell
# 预览会新增哪些
python pcl_extend.py servers

# 探测 Java 服务器在线状态（Server List Ping）
python pcl_extend.py servers --status

# 合并并备份为 servers.dat.bak-YYYYMMDD-HHMMSS
python pcl_extend.py servers --apply
```

`--game-dir` 可以指定别的 Minecraft 目录；不指定时用 `%APPDATA%\.minecraft`。

## 安全设计

- Mod 安装默认 **dry-run**，只打印文件名；
- 服务器写入前自动 `servers.dat.bak-<时间戳>`；
- 已存在的 jar 不重复下载；服务器按 `ip` 去重；
- 不修改 PCL Pro 本体，不注入进程，不改协议。

## 与手机版的关系

手机版（`PCL Pro 手机版/mobile-fork`）已有完整的扩展中心 UI、全网搜索和依赖解析；
桌面端这份脚本复用同一套 JSON 数据，是“没有源码时能做到的最小侵入集成”。

桌面端现状：`26.3-Fabric` 实例，`mods/` 里已有 Sodium 与 ViaFabricPlus；
Modrinth 对 26.3/fabric 有构建（Sodium 0.9.3、Lithium 0.26.4、FerriteCore 9.0.0 等）。
