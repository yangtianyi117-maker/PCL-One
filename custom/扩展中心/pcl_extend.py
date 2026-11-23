#!/usr/bin/env python3
"""PCL Pro 桌面端旁路扩展（无第三方依赖）。

PCL Pro 本体是编译后的 WPF 程序，不能改它的 UI；但它的游戏目录是标准 Minecraft
目录，所以可以从外部做两件事：

  mods    从 Modrinth 解析并安装客户端 Mod（含 required 前置）到 <game>/mods
  servers 把 pcl_servers.json 里的 Java 服务器合并进 <game>/servers.dat（带备份）

第一性：客户端 Mod 只改变本地客户端，服务端无需安装；服务器列表是纯数据文件。
这两件事都不碰 PCL Pro 进程，也不碰协议。

用法：
  python pcl_extend.py mods --list
  python pcl_extend.py mods --install sodium lithium --apply
  python pcl_extend.py mods --install --all --apply
  python pcl_extend.py servers            # 只预览
  python pcl_extend.py servers --apply    # 合并并备份
"""

import argparse
import gzip
import hashlib
import json
import os
import re
import shutil
import struct
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent
API = "https://api.modrinth.com/v2"
USER_AGENT = "PCLProDesktop/1.0 (local extension helper)"


# --------------------------------------------------------------------------- #
# 基础工具
# --------------------------------------------------------------------------- #

def log(message):
    print(message, flush=True)


def load_json(name):
    return json.loads((ROOT / name).read_text(encoding="utf-8"))


def default_game_dir():
    if os.name == "nt" and os.environ.get("APPDATA"):
        return Path(os.environ["APPDATA"]) / ".minecraft"
    return Path.home() / ".minecraft"


def http_json(path, params=None):
    url = API + path
    if params:
        url += "?" + urllib.parse.urlencode(params)
    request = urllib.request.Request(url, headers={
        "User-Agent": USER_AGENT,
        "Accept": "application/json",
    })
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


# --------------------------------------------------------------------------- #
# Java Server List Ping（只读查询）
# --------------------------------------------------------------------------- #

def _varint(value):
    out = b""
    while True:
        bits = value & 0x7F
        value >>= 7
        if value:
            out += bytes([bits | 0x80])
        else:
            return out + bytes([bits])


def _read_varint(sock):
    value = 0
    position = 0
    while True:
        current = sock.recv(1)
        if not current:
            raise IOError("eof")
        current = current[0]
        value |= (current & 0x7F) << position
        if not (current & 0x80):
            return value
        position += 7


def java_ping(host, port=25565, timeout=2.5):
    import socket
    import struct
    import time as _time

    try:
        sock = socket.create_connection((host, port), timeout)
        sock.settimeout(timeout)
        handshake = (_varint(0) + _varint(47)
                     + _varint(len(host.encode("utf-8"))) + host.encode("utf-8")
                     + struct.pack(">H", port) + _varint(1))
        sock.sendall(_varint(len(handshake)) + handshake + _varint(1) + _varint(0))
        start = _time.time()
        length = _read_varint(sock)
        data = b""
        while len(data) < length:
            chunk = sock.recv(length - len(data))
            if not chunk:
                break
            data += chunk
        sock.close()
        offset = 0
        packet_id = 0
        position = 0
        while True:
            current = data[offset]
            offset += 1
            packet_id |= (current & 0x7F) << position
            if not (current & 0x80):
                break
            position += 7
        if packet_id != 0:
            return None
        strlen = 0
        position = 0
        while True:
            current = data[offset]
            offset += 1
            strlen |= (current & 0x7F) << position
            if not (current & 0x80):
                break
            position += 7
        payload = json.loads(data[offset:offset + strlen].decode("utf-8"))
        players = payload.get("players", {})
        return {
            "online": players.get("online"),
            "max": players.get("max"),
            "version": payload.get("version", {}).get("name"),
            "ms": round((_time.time() - start) * 1000),
        }
    except Exception:
        return None


def detect_instance(game_dir, mc=None, loader=None):
    """从 PCL.ini 的 Version: 行推断 MC 版本与加载器（例如 26.3-Fabric）。"""
    if mc and loader:
        return mc, loader
    ini = game_dir / "PCL.ini"
    if ini.exists():
        text = ini.read_text(encoding="utf-8", errors="ignore")
        match = re.search(r"^Version:(.+)$", text, re.MULTILINE)
        if match:
            version_id = match.group(1).strip()
            for name in ("Fabric", "Quilt", "NeoForge", "Forge"):
                suffix = "-" + name
                if version_id.endswith(suffix):
                    return version_id[:-len(suffix)], name.lower()
            return version_id, "vanilla"
    return None, None


# --------------------------------------------------------------------------- #
# NBT（servers.dat）：只实现需要的读/写，标准库即可
# --------------------------------------------------------------------------- #

class _Reader:
    def __init__(self, data):
        self.data = data
        self.offset = 0

    def u8(self):
        value = self.data[self.offset]
        self.offset += 1
        return value

    def i16(self):
        value = struct.unpack_from(">h", self.data, self.offset)[0]
        self.offset += 2
        return value

    def i32(self):
        value = struct.unpack_from(">i", self.data, self.offset)[0]
        self.offset += 4
        return value

    def i64(self):
        value = struct.unpack_from(">q", self.data, self.offset)[0]
        self.offset += 8
        return value

    def f32(self):
        value = struct.unpack_from(">f", self.data, self.offset)[0]
        self.offset += 4
        return value

    def f64(self):
        value = struct.unpack_from(">d", self.data, self.offset)[0]
        self.offset += 8
        return value

    def string(self):
        length = struct.unpack_from(">H", self.data, self.offset)[0]
        self.offset += 2
        value = self.data[self.offset:self.offset + length].decode("utf-8", errors="replace")
        self.offset += length
        return value

    def payload(self, tag):
        if tag == 1:
            return self.u8()
        if tag == 2:
            return self.i16()
        if tag == 3:
            return self.i32()
        if tag == 4:
            return self.i64()
        if tag == 5:
            return self.f32()
        if tag == 6:
            return self.f64()
        if tag == 7:
            length = self.i32()
            self.offset += length
            return b""
        if tag == 8:
            return self.string()
        if tag == 9:
            element = self.u8()
            length = self.i32()
            return [self.payload(element) for _ in range(length)]
        if tag == 10:
            result = {}
            while True:
                child = self.u8()
                if child == 0:
                    return result
                name = self.string()
                result[name] = self.payload(child)
        if tag == 11:
            length = self.i32()
            self.offset += length * 4
            return []
        if tag == 12:
            length = self.i32()
            self.offset += length * 8
            return []
        raise ValueError("unsupported NBT tag: %d" % tag)


def _nbt_string(value):
    encoded = value.encode("utf-8")
    return struct.pack(">H", len(encoded)) + encoded


def _tag_value(tag, value):
    if tag == 1:
        return struct.pack(">b", int(value))
    if tag == 3:
        return struct.pack(">i", int(value))
    if tag == 8:
        return _nbt_string(str(value))
    raise ValueError("unsupported write tag: %d" % tag)


def _key(tag, name):
    return bytes([tag]) + _nbt_string(name)


def _write_root(servers):
    out = bytearray(b"\x0a\x00\x00")  # TAG_Compound, empty root name
    out += _key(9, "servers") + bytes([10]) + struct.pack(">i", len(servers))
    for server in servers:
        body = bytearray()
        body += _key(1, "hidden") + struct.pack(">b", int(server.get("hidden", 0)))
        body += _key(8, "ip") + _nbt_string(server.get("ip", ""))
        body += _key(8, "name") + _nbt_string(server.get("name", "Server"))
        body += b"\x00"
        out += body
    out += b"\x00"
    return bytes(out)


def read_servers(path):
    if not path.exists():
        return []
    raw = path.read_bytes()
    if raw[:2] == b"\x1f\x8b":
        raw = gzip.decompress(raw)
    reader = _Reader(raw)
    tag = reader.u8()
    if tag != 10:
        raise ValueError("servers.dat root is not a compound")
    reader.string()
    root = reader.payload(10)
    return root.get("servers", [])


def write_servers(path, servers):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + ".new")
    temp.write_bytes(_write_root(servers))
    os.replace(temp, path)


# --------------------------------------------------------------------------- #
# Modrinth：解析版本、下载、递归前置
# --------------------------------------------------------------------------- #

def versions_for(project, mc, loader):
    return http_json("/project/%s/version" % project, {
        "game_versions": json.dumps([mc]),
        "loaders": json.dumps([loader]),
    })


def download(url, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.stat().st_size > 0:
        log("  = %s（已存在）" % destination.name)
        return
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    temp = destination.with_name(destination.name + ".part")
    with urllib.request.urlopen(request, timeout=60) as response, open(temp, "wb") as out:
        shutil.copyfileobj(response, out)
    temp.replace(destination)
    log("  + %s（%.1f KB）" % (destination.name, destination.stat().st_size / 1024))


def install_version(version, dest, dry, visited, mc, loader):
    project_id = version.get("project_id")
    if project_id and project_id in visited:
        return
    if project_id:
        visited.add(project_id)

    files = version.get("files") or []
    if not files:
        return
    file = files[0]
    filename = file.get("filename") or "mod.jar"
    if dry:
        log("  ~ %s" % filename)
    else:
        download(file.get("url"), dest / filename)

    for dependency in version.get("dependencies", []):
        if dependency.get("dependency_type") != "required":
            continue
        if dependency.get("version_id"):
            child = http_json("/version/%s" % dependency["version_id"])
            install_version(child, dest, dry, visited, mc, loader)
        elif dependency.get("project_id"):
            install_project(dependency["project_id"], dest, dry, visited, mc, loader)


def install_project(project, dest, dry, visited, mc, loader):
    if project in visited:
        return
    versions = versions_for(project, mc, loader)
    if not versions:
        log("  ! %s：没有 %s/%s 的构建" % (project, mc, loader))
        return
    install_version(versions[0], dest, dry, visited, mc, loader)


def sha1_file(path):
    digest = hashlib.sha1()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(8192), b""):
            digest.update(chunk)
    return digest.hexdigest()


def scan_installed(dest, mc, loader):
    """Return installed jars enriched with Modrinth version info (when the hash is known)."""
    result = []
    if not dest.exists():
        return result
    for path in sorted(dest.glob("*.jar")):
        info = {"path": path, "name": path.name, "size": path.stat().st_size}
        info["update"] = False
        try:
            sha1 = sha1_file(path)
            version = http_json("/version_file/" + sha1)
            info["current"] = version.get("version_number")
            project_id = version.get("project_id")
            info["project_id"] = project_id
            latest = versions_for(project_id, mc, loader)
            if latest:
                top = latest[0]
                info["latest"] = top.get("version_number")
                files = top.get("files") or []
                if files:
                    info["url"] = files[0].get("url")
                    info["filename"] = files[0].get("filename")
                info["update"] = bool(
                    info.get("latest") and info.get("current")
                    and info["latest"] != info["current"]
                )
        except Exception:
            pass
        result.append(info)
    return result


def cmd_mods(args):
    game_dir = Path(args.game_dir) if args.game_dir else default_game_dir()
    mc, loader = detect_instance(game_dir, args.mc, args.loader)
    if not mc or not loader or loader == "vanilla":
        log("无法从 PCL.ini 推断实例；请用 --mc 和 --loader 指定")
        return 1
    log("实例：%s / %s" % (mc, loader))

    catalog = load_json("pcl_client_mods.json")
    all_mods = []
    for category in catalog.get("categories", []):
        all_mods.extend(category.get("mods", []))

    if args.installed or args.updates or args.update is not None:
        dest = Path(args.dest) if args.dest else game_dir / "mods"
        installed = scan_installed(dest, mc, loader)
        updates = [item for item in installed if item.get("update")]
        if args.update is not None:
            wanted = set(args.update) if args.update else None
            changed = 0
            for item in updates:
                if wanted is not None and item["name"] not in wanted:
                    continue
                if not item.get("url") or not item.get("filename"):
                    continue
                log("[更新] %s：%s → %s" % (item["name"],
                                           item.get("current"), item.get("latest")))
                download(item["url"], dest / item["filename"])
                new_path = (dest / item["filename"]).resolve()
                if new_path != item["path"].resolve():
                    item["path"].unlink(missing_ok=True)
                changed += 1
            log("更新 %d 个" % changed)
            return 0

        listing = updates if args.updates else installed
        for item in listing:
            if item.get("update"):
                status = "%s → %s" % (item.get("current"), item.get("latest"))
            elif item.get("current"):
                status = "%s（已是最新）" % item["current"]
            else:
                status = "未知来源"
            log("  %-48s %7.0f KB  %s" % (item["name"], item["size"] / 1024, status))
        log("已安装 %d 个，可更新 %d 个" % (len(installed), len(updates)))
        return 0

    if args.search is not None:
        facets = '[[\"project_type:mod\"],[\"client_side:required\",\"client_side:optional\"]'
        if loader and loader != "vanilla":
            facets += ',[\"versions:%s\"]' % mc
            facets += ',[\"categories:%s\"]' % loader
        facets += ']'
        try:
            data = http_json("/search", {
                "query": args.search,
                "limit": 20,
                "index": "relevance",
                "facets": facets,
            })
        except Exception as error:
            log("搜索失败：%s" % error)
            return 1
        hits = data.get("hits", [])
        for hit in hits:
            log("  %-30s %s" % (hit.get("slug"), hit.get("title")))
        log("共 %d 条；用 --install <slug> --apply 安装" % len(hits))
        return 0

    if args.list:
        found = 0
        for mod in all_mods:
            try:
                versions = versions_for(mod["slug"], mc, loader)
            except Exception as error:
                log("  ? %-28s %s" % (mod["slug"], error))
                continue
            if versions:
                found += 1
                log("  + %-28s %s" % (mod["slug"], versions[0]["name"]))
            else:
                log("  - %-28s 无匹配构建" % mod["slug"])
        log("可安装 %d / %d" % (found, len(all_mods)))
        return 0

    if not args.install and not args.all:
        log("用 --list 查看，或 --install <slug...> / --all")
        return 1

    dest = Path(args.dest) if args.dest else game_dir / "mods"
    dry = not args.apply
    if dry:
        log("（预览模式；加 --apply 才会写入 %s）" % dest)

    selected = all_mods if args.all else []
    if args.install:
        wanted = set(args.install)
        selected = [m for m in all_mods if m["slug"] in wanted]
        missing = wanted - {m["slug"] for m in selected}
        for slug in sorted(missing):
            selected.append({"slug": slug, "name": slug})

    visited = set()
    for mod in selected:
        log("[%s]" % mod["slug"])
        try:
            install_project(mod["slug"], dest, dry, visited, mc, loader)
        except Exception as error:
            log("  ! %s：%s" % (mod["slug"], error))
    log("完成%s" % ("（预览）" if dry else ""))
    return 0


# --------------------------------------------------------------------------- #
# 服务器合并
# --------------------------------------------------------------------------- #

def cmd_servers(args):
    game_dir = Path(args.game_dir) if args.game_dir else default_game_dir()
    path = game_dir / "servers.dat"
    existing = read_servers(path)
    curated = [s for s in load_json("pcl_servers.json").get("servers", [])
               if s.get("edition") == "java"]

    if args.status:
        for server in curated:
            host = str(server.get("address", "")).strip()
            port = 25565
            if ":" in host:
                host, port_text = host.rsplit(":", 1)
                try:
                    port = int(port_text)
                except ValueError:
                    pass
            result = java_ping(host, port)
            if result:
                log("  %-18s %s/%s  %sms" % (server.get("name"), result["online"],
                                            result["max"], result["ms"]))
            else:
                log("  %-18s 离线 / 无响应" % server.get("name"))
        return 0

    known = {str(s.get("ip", "")).strip().lower() for s in existing}
    added = []
    for server in curated:
        ip = str(server.get("address", "")).strip()
        if not ip or ip.lower() in known:
            continue
        known.add(ip.lower())
        added.append({"name": server.get("name", ip), "ip": ip, "hidden": 0})

    log("现有服务器：%d" % len(existing))
    log("可新增：%d" % len(added))
    for server in added:
        log("  + %s  %s" % (server["name"], server["ip"]))
    if not added:
        log("没有需要新增的条目")
        return 0
    if not args.apply:
        log("（预览模式；加 --apply 才会写入并备份）")
        return 0

    if path.exists():
        backup = path.with_name("servers.dat.bak-%s" % time.strftime("%Y%m%d-%H%M%S"))
        shutil.copy2(path, backup)
        log("已备份：%s" % backup)
    write_servers(path, existing + added)
    log("已写入：%s（共 %d 个服务器）" % (path, len(existing) + len(added)))
    return 0


def main():
    parser = argparse.ArgumentParser(description="PCL Pro 桌面端旁路扩展")
    parser.add_argument("--game-dir", help="Minecraft 游戏目录（默认 %%APPDATA%%\\.minecraft）")
    sub = parser.add_subparsers(dest="command", required=True)

    mods = sub.add_parser("mods", help="列出/安装客户端 Mod")
    mods.add_argument("--list", action="store_true")
    mods.add_argument("--search", help="在 Modrinth 搜索客户端 Mod")
    mods.add_argument("--install", nargs="*")
    mods.add_argument("--all", action="store_true")
    mods.add_argument("--installed", action="store_true", help="列出 <dest> 下已安装的 jar 与版本")
    mods.add_argument("--updates", action="store_true", help="只列出可更新的 jar")
    mods.add_argument("--update", nargs="*", help="更新全部或指定的 jar")
    mods.add_argument("--apply", action="store_true", help="真正下载（默认只预览）")
    mods.add_argument("--dest", help="安装目录（默认 <game>/mods）")
    mods.add_argument("--mc")
    mods.add_argument("--loader")

    servers = sub.add_parser("servers", help="合并公共服务器到 servers.dat")
    servers.add_argument("--apply", action="store_true", help="真正写入并备份")
    servers.add_argument("--status", action="store_true", help="探测 Java 服务器在线状态")

    args = parser.parse_args()
    if args.command == "mods":
        return cmd_mods(args)
    return cmd_servers(args)


if __name__ == "__main__":
    sys.exit(main())
