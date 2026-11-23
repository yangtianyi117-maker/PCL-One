# Towny 覆盖项（v1 默认）

Towny 首次启动后会生成 `plugins/Towny/settings/` 下的一整套 YAML；不要整份替换，按下面的键改值。
键名以你安装的 Towny 版本为准，改完用 `/towny admin` 或重启验证。

## 经济与价格

| 键（大致） | 值 | 说明 |
|---|---|---|
| `using_economy` | `true` | 需要 Vault + EssentialsX |
| `starting_bank_balance` | `0` | 国家/城镇金库初始为 0 |
| `town_cost` | `1000` | 建镇 |
| `nation_cost` | `5000` | 建国 |
| `claim_price` | `10` | 每格 |
| `max_town_claims` | `256` | 单镇上限 |
| `town_tax` / `nation_tax` | `10` | 每日税 |

## 战争

| 键（大致） | 值 |
|---|---|
| `war.enabled` | `true` |
| `war.allow_attacks` | `true` |
| `war.warring_nations` | `true` |
| `war.block_war_against_ally` | `true` |

## 出生点

- `/setworldspawn 3233 64 -4826`（伊斯坦布尔）
- `spawn-protection=32`
- 可选：建一个 `Spawn` 城镇并 claim 出生平台，禁止 PvP。

## 验证

```text
/town new TestTown
/town claim
/nation new TestNation
/town set nation TestNation
/towny war declare TestNation OtherNation
```

跑通后把测试数据删掉：`/town delete TestTown`。
