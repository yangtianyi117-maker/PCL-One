# 安全策略

## 不在仓库内的东西

| 资产 | 位置 | 说明 |
|---|---|---|
| 更新签名私钥（ECDSA P-256） | 本地 `secrets/pclpro-update-key.json` / CI Secrets | 泄露即需换钥并重发所有版本 |
| VPS 凭据 | 环境变量 `PCL_VPS_PASSWORD` | 不入库、不写日志 |
| 服务器 token（frp 等） | 服务器本地配置 | 不随仓库分发 |

公开代码中的更新公钥（`ProUpdateService.PublicKeyX/Y`）是**公钥**，用于客户端验签，可以公开。

## 报告漏洞

请**不要**开公开 issue。通过仓库维护者的私下渠道（邮件/私信）报告，附复现步骤与影响范围。

## 构建发布要求

- 发布产物必须用 `-PublicRelease` 构建，确保包含正版购买劝导；
- 签名私钥只在受控环境使用，产物需附 `update.json` + `.sig`；
- 发布前跑一遍 `tools/verify-single-install.ps1`（若适用）与启动冒烟测试。
