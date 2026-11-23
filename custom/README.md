# PCL Custom

[![build](https://github.com/yangtianyi117-maker/PCL-Custom/actions/workflows/build.yml/badge.svg)](https://github.com/yangtianyi117-maker/PCL-Custom/actions/workflows/build.yml)
[中文](README.zh-CN.md)

> An independent third-party derivative of [PCL](https://github.com/Meloong-Git/PCL) and
> [PCL CE](https://github.com/PCL-Community/PCL-CE).
> **Not affiliated with, nor an official successor of, PCL.** Original author: 龙腾猫跃 · Sponsor: <https://afdian.com/a/LTCat>
>
> Chinese name: **PCL 定制版**. `PCL Pro` was the working name; under the upstream PCL Repository Fair Use Guide the public name is **PCL Custom**. Some internal design notes still use the old codename.

## What this is

A Minecraft Java Edition launcher. On top of the upstream codebase it adds a few engineering changes:

| Capability | Description |
|---|---|
| **Server list** | Pick a server the way you pick a mod: one row per server, live ping/player count, "Join" connects directly, "Copy" gives the Bedrock address |
| **Mod inbox** | Drop jars into `PCL\ModInbox`; the loader/version is detected and installed, missing `required` dependencies are fetched from Modrinth |
| **Signed auto-update** | ECDSA P-256 manifest + exe SHA-256 check, Range-resumable download, replaces only the bootstrapper |
| **Portable mode** | All writable data lives in `PCL\` next to the exe; `%AppData%` is left untouched |
| **UI cleanup** | Launch popups and redundant entries removed, server entry pinned to the top |

Everything else (download, install, modpacks, skins, crash analysis, …) comes from upstream.

## PCL One — offline native launcher (5.0.0)

[PCL-One](https://github.com/yangtianyi117-maker/PCL-One) is the first-party offline distribution: no third-party launcher UI,
direct `version JSON → classpath → java` launch, and an in-game Fabric menu
(`pcl-one-menu`) on the 26.3 client. It packages Earth 26.3, Titan 1.7.10/1.12.2,
the 1.7.10 Forge server, six shortcuts and a silent single-file installer.

- Source: [PCL-One](https://github.com/yangtianyi117-maker/PCL-One)
- Design: [OFFLINE.md](https://github.com/yangtianyi117-maker/PCL-One/blob/main/docs/OFFLINE.md) and [DESIGN.md](https://github.com/yangtianyi117-maker/PCL-One/blob/main/docs/DESIGN.md)
- Build: `powershell -File build.ps1 -Verify` in the PCL-One repo (needs `vendor/` binaries, not in git)
- Android link helper sources: `src/mobile` in PCL-One (mirrored to
  [PCL-Mobile-Link](https://github.com/yangtianyi117-maker/PCL-Mobile-Link))

The launcher binary (`PCLPro.exe`, extension center, signed update) stays in this
repository; PCL One is the self-contained distribution built on top of the same
Minecraft runtimes.

## Extension Center / 扩展中心

`PCLPro.exe` is a compiled binary with no source, so the desktop build ships a **sidecar tool**
that works without touching the launcher process:

- **Client mods** — resolve Modrinth builds for the current instance (required dependencies
  included) and download them into `<game dir>/mods`;
- **Server list** — merge `pcl_servers.json` into `<game dir>/servers.dat` with an automatic
  `servers.dat.bak-<timestamp>` backup.

Python 3 standard library only; mod installs are dry-run by default. See
[`扩展中心/README.md`](扩展中心/README.md) for the full command list.

## Download

If you don't want to build it, grab `PCL-Custom-v1.1.0-win64.zip` from
[Releases](https://github.com/yangtianyi117-maker/PCL-Custom/releases/latest)
(Windows x64; unzip and run `PCLPro.exe`).

- SHA-256: `F851C0EC1669D912FEEDDCBEF6F4158FD6574CBC594AADCAD588E0E3609774FC`
- China mirror (same file, same SHA-256): <http://124.221.231.213/pclpro/PCL-Custom-v1.1.0-win64.zip>

### Slow downloads from mainland China / 国内下载慢

GitHub release assets are served by Fastly; routes through a VPN are often slower than the
mirror. For China, use the mirror first — it is hosted on a Tencent Cloud VPS in mainland China
and supports HTTP Range (resume):

```bash
aria2c -x 8 -s 8 -c -o PCL-Custom-v1.1.0-win64.zip \
  http://124.221.231.213/pclpro/PCL-Custom-v1.1.0-win64.zip
```

The mirror uplink is about 5 Mbps (~600 KB/s), so 117 MB takes roughly 3–4 minutes. If you have
a fast international route, GitHub may be faster; if a VPN makes it worse, switch node or use a
GitHub proxy such as `https://ghproxy.net/https://github.com/...`.

Always verify the SHA-256 above after downloading.

## Build

Prerequisites: Windows x64, [.NET 10 SDK](https://dotnet.microsoft.com/), [Zig 0.13](https://ziglang.org/).

Tools are resolved as: explicit parameter → in-repo `tools\dotnet-sdk` / `tools\zig-windows-x86_64-0.13.0` → `PATH`.
So a fresh clone builds as long as `dotnet` and `zig` are on your `PATH` (the SDK and Zig are not committed). Sanity check first:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1 -DryRun
```

Then build:

```powershell
# Development build (no purchase prompt; personal use only)
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1

# Public release build (required by the license)
powershell -ExecutionPolicy Bypass -File tools\build-pclpro.ps1 -PublicRelease
```

Output: `dist\PCLPro.exe` (bootstrapper) which unpacks `PCLProRuntime\` on first run.

> The project requires `LangVersion=14` (.NET 10 default); the build script already passes it.
>
> **Microsoft account login** needs an OAuth client ID injected at build time (PCL_MS_CLIENT_ID). The repo does not ship one; public builds without it cannot sign in with a Microsoft account, and the build script refuses `-PublicRelease` unless you pass `-AllowNoMsClientId`.
> The EasyTier directory (`tools\easytier\pcl-src`) is optional; if missing, the script skips it with a warning and the launcher can download it on demand.

## Layout

```
tools/pclce/PCL-CE-dev/   launcher source (upstream + this project's changes)
tools/bootstrapper/       Zig-written exe shell: unpack, forward args, self-update
tools/research/           multiplayer server collection scripts (see docs/research)
扩展中心/                 sidecar tool: Modrinth client mods + servers.dat merge
docs/                     design and research notes
```

## License

- Root `LICENSE`: Apache-2.0 (everything outside `Plain Craft Launcher 2/`)
- `LICENCE`: *PCL Distribution Limited License* + *PCL Repository Fair Use Guide* (for `Plain Craft Launcher 2/`)
- Changes made by this project are bound by the same licenses; public release builds must enable the
  original-purchase prompt (`-PublicRelease`).

## Security

Private keys and server credentials are not in this repository. See [SECURITY.md](SECURITY.md).

## Disclaimer

Provided "as is", without warranty of any kind. Obey the Minecraft EULA and your local laws.
