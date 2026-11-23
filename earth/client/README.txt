PCL Earth client package
=======================

Install
-------
1. Make sure PCL Pro is installed (this package does NOT include PCLPro.exe).
2. Double-click "安装 Earth.bat" (or run install-earth.ps1 with ExecutionPolicy Bypass).
3. Launch PCL Pro (or "PCL Earth.bat" in the PCL Pro folder). The game should
   auto-join the Earth server.

Offline
-------
The eight mod jars ship in payload/mods and are copied + sha1-verified locally.
No Modrinth query, no download. Pass -AllowDownload only if you want the old
online fallback for a missing jar.

Server
------
Java:    124.221.231.213:25566
Bedrock: 124.221.231.213:19132

Uninstall
---------
Run uninstall-earth.ps1. It restores PCL.ini / servers.dat / 26.3-Fabric.json
from the backup created at install time and moves the Earth instance aside.

Notes
-----
- Mods: Fabric API, Sodium, Lithium, FerriteCore, Xaero's Minimap/World Map,
  Mod Menu, Cloth Config. Pinned in shared/mods.json.
- No Minecraft world files are included in this package, ever.
- Map data is licensed to the server operator only; it is not redistributed.

Licenses
--------
PCL / PCL Pro: custom license, source-available, not open source. This package
does not modify or redistribute the PCL binary.

Xaero's Minimap / World Map: All Rights Reserved by Xaero96.
https://modrinth.com/mod/xaeros-minimap
https://modrinth.com/mod/xaeros-world-map

Minecraft is a trademark of Mojang/Microsoft. This is an unofficial community
server and package, not affiliated with Mojang, Microsoft, or PCL's author.

Earth map: EarthMC Full Earth (2026) 1:1000, licensed for server operation only;
world files must remain private.
