# PCL Pro 自动更新

## 现状

- 更新源：`http://124.221.231.213/pclpro/`
- 托管：VPS nginx 静态目录 `/var/www/mc/pclpro/`
- 客户端：`PCL.Core/Updates/ProUpdateService.cs` + `Plain Craft Launcher 2/Link/ProUpdateRunner.cs`
- 信任锚：内置 ECDSA P-256 公钥
  - 指纹：`b9398c39662c770a441248eba446f30bd7dbfb199fc7444e286e2ef1795dff25`
- 策略 A：启动时静默下载、验签、替换 `PCL Pro\PCLPro.exe`，提示“重启生效”，不打断游戏。

## 客户端流程

```
启动 → 读 LinkProUpdateUrl（默认 http://124.221.231.213/pclpro/）
     → GET update.json + update.json.sig
     → 内置公钥验签
     → buildNumber > 当前 ProEdition.BuildNumber ?
     → 下载 payloadUrl 到 PCLPro.exe.new
     → 校验 size + SHA-256
     → 备份 PCLPro.exe.old，原子替换 PCLPro.exe
     → 提示“已下载，重启生效”
```

只替换 bootstrapper；运行中的 `PCLProRuntime` 不动。下次启动 bootstrapper 发现
`.payload-version` 变化，自动重解压代码，`PCL/` 数据保留。

## 服务端

nginx 站点 `root /var/www/mc`，`location / { try_files ... }`，
所以 `/var/www/mc/pclpro/` 下的文件直接通过 `http://124.221.231.213/pclpro/` 提供。

目录：

```
/var/www/mc/pclpro/
  update.json
  update.json.sig
  PCLPro.exe
```

上传：

```powershell
$env:PCL_VPS_PASSWORD = '...'   # 或改用 SSH key，不要把密码写进脚本
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-pclpro-update.ps1 `
  -Version 2026-11-20.10 -BuildNumber 10
```

## 发版步骤

1. 改代码，递增 `PCL.Core/App/ProEdition.cs` 的 `BuildNumber/BuildId`；
2. 改 `tools/bootstrapper/main.zig` 的 `payload_version`；
3. `tools\build-pclpro.ps1` 构建 `dist\PCLPro.exe`；
4. `tools\publish-pclpro-update.ps1 -Version ... -BuildNumber ...` 签名并上传；
5. 客户端下次启动自动更新。

## 已验证

- 伪造 build 10 清单：客户端日志
  `[ProUpdate] 发现新版本 ... → 下载 → 已替换 bootstrapper`，
  生成 `PCLPro.exe.old`，提示重启。
- 恢复 build 9 清单后不再重复更新。
- 下载走 HTTP；完整性/真实性由 manifest 签名 + exe SHA-256 保证。
- 若签名无效、SHA 不符、大小不符：拒绝替换，保留旧版。
- 断点续传实测：本地 TCP 服务器首包只发 1 MB 后断开，客户端第二次用 Range 从 1 MB 续传，
  最终 SHA-256 匹配并替换；测试在 `tools/test/ProUpdateRetryTest`。

## 边界

- HTTP 不提供保密性；签名只保证完整性/真实性。
- 公钥写死在客户端；换钥必须发一次带新公钥的手动包。
- 首次从旧版升级到 `.9` 仍需手动替换；`.9` 之后才自动。
- Windows SmartScreen 仍需 Authenticode 证书才能消除。
