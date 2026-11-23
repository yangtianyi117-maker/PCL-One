# PCL Pro 更新包签名（第一性）

## 1. 威胁模型

更新包从 A 发到 B，中间可能被网盘/聊天工具/第三方替换。需要保证两件事：

- **完整性**：字节没被改过；
- **真实性**：确实由持有某个私钥的人发布。

密码学只能解决这两件事，不能解决“B 的电脑已经被控制”“A 的私钥泄露”“Windows 不信任这个 exe”。

## 2. 结构

```
PCLPro-update-2026-11-21.8-signed/
  PCLPro.exe              实际启动器
  update.json             版本 + exe 的 SHA-256 + 大小 + 发布时间
  update.json.sig         ECDSA P-256 / SHA-256 对 update.json 原始字节的签名
  verify-update.ps1       内置公钥的验证脚本
  key-fingerprint.txt     公钥指纹（用于线下核对）
  UPDATE-NOTES.txt        使用说明
```

验证顺序：

1. 用内置公钥验证 `update.json.sig` 对 `update.json` 原始字节的签名；
2. 解析 `update.json`，计算 `PCLPro.exe` 的 SHA-256；
3. 与 `update.json.sha256` 比较，并校验大小；
4. 全部通过才提示 PASS。

签 manifest 而不是直接签 exe 的原因：manifest 可以把版本、哈希、发布时间、备注绑在一起；exe 只需校验哈希。

## 3. 信任锚

`verify-update.ps1` 里内置公钥；公钥指纹：

```
b9398c39662c770a441248eba446f30bd7dbfb199fc7444e286e2ef1795dff25
```

这个指纹必须通过**另一条可信渠道**核对（例如聊天里单独发一句，而不是从同一个压缩包读）。
如果攻击者同时改掉脚本、公钥和指纹，只有你之前记过指纹才能发现。

私钥保存在发布者本机 `secrets/pclpro-update-key.json`，**不随包分发**。泄露后必须换钥并重新公布指纹。

## 4. 生成与验证

```powershell
# 生成签名包
powershell -NoProfile -ExecutionPolicy Bypass -File tools\sign-pclpro-update.ps1

# 接收方验证
powershell -NoProfile -ExecutionPolicy Bypass -File .\verify-update.ps1
```

已验证：

- 正常包：`PASS PCL Pro 2026-11-21.8`
- 改 exe 一个字节：`FAIL payload sha256 mismatch`
- 改 update.json：`FAIL signature invalid`

## 5. 局限

- **不会消除 SmartScreen**。那需要 CA 签发的 Authenticode 代码签名证书；自签名证书在别人机器上仍会警告，除非对方手动导入证书。
- **不会自动更新**。当前仍是“验证后手动替换 exe”；要做到自动更新，需要在启动器/bootstrapper 里内置公钥并在应用前验签，这是下一步。
- **签名不等于安全审计**。它只证明来源与完整性；启动器本身的行为仍需信任发布者。
- 完整包（含 `.minecraft`）目前只签 exe；如果要签整个包，需要把 zip 哈希也放进 manifest。

## 6. 当前产物

- 签名包：`dist/PCLPro-update-2026-11-21.8-signed.zip`
- zip SHA-256：`FA27710BA2496C9992B7FF3F51E3DD7CC8EAC0506001F54A12B017F80C590F01`
- exe SHA-256：`B39824BFFA31A005584BFAD43AFADD014E3503200D0006697C0C3D527660D8CF`
- 公钥指纹：`b9398c39662c770a441248eba446f30bd7dbfb199fc7444e286e2ef1795dff25`
- 自动更新源：`http://124.221.231.213/pclpro/`
- 自动更新细节见 `docs/MERGE/AUTO-UPDATE.md`。
