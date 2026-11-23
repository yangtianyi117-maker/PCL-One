# 验收清单（PCL Earth v1）

> 前置：地图已购买并解压为 `Earth1000/`；服务端可 SSH；客户端已装 PCL Pro。

## A. 地图与坐标

- [ ] `Earth1000/level.dat` 存在，`datapacks/`、`data/minecraft/world_gen_settings.dat` 保留。
- [ ] Paper 26.3 能加载存档（先 staging），`/tps` 正常。
- [ ] 逐点 `/tp` 检查：
  - [ ] 伊斯坦布尔 `(3233, ~64, -4826)`：博斯普鲁斯海峡、两岸陆地
  - [ ] 北京 `(12986, ~64, -4664)`：华北平原
  - [ ] 纽约 `(-8256, ~64, -4782)`：北美东岸
  - [ ] 开罗 `(3484, ~64, -3320)`：尼罗河三角洲
  - [ ] 里约 `(-4816, ~64, 2815)`：海岸线
  - [ ] 悉尼 `(16868, ~64, 4207)`：悉尼港
- [ ] 误差 ≤ 1 区块；否则停止上线，核对地图版本/投影。

## B. 出生点与边界

- [ ] `/setworldspawn 3233 64 -4826` 后新玩家在石台出生。
- [ ] `/gamerule spawnRadius 0`；`spawn-protection=32`。
- [ ] ChunkyBorder 矩形边界生效：向外走被拦。
- [ ] 原版 `/worldborder` 不是唯一边界（不要用它替代）。

## C. 双端互通

- [ ] Java 客户端进服，放一个明显方块（如金块）。
- [ ] Bedrock 客户端（Geyser 19132）进服，能看到该方块。
- [ ] 两边玩家列表、聊天互通。
- [ ] Bedrock 版本与 Geyser 兼容（错误时看 Geyser 日志的 `bedrock` 版本提示）。

## D. 客户端包

- [ ] `install-earth.ps1` 在干净用户上成功；`Earth-26.3-Fabric` 实例出现。
- [ ] 启动 PCL Pro 自动进服（quickPlay）。
- [ ] `servers.dat` 列表出现「Earth 1:1000」。
- [ ] Xaero 世界地图按 `M` 可打开；已探索区域正常绘制。
- [ ] 卸载脚本还原 `PCL.ini` / `servers.dat` / 原实例 JSON。
- [ ] 客户端 ZIP 审计：无 `region/`、`level.dat`、`.mca`。

## E. 规则

- [ ] `/town new TestTown`、`/town claim`、`/nation new TestNation` 成功。
- [ ] 战争宣战/停战可执行；出生点免战。
- [ ] AuthMe 注册/登录；Floodgate 玩家能进。
- [ ] CoreProtect 能回滚一次测试破坏。

## F. 性能与恢复

- [ ] 20 人在线 TPS ≥ 19.5（或 10 人 + Chunky 预生成）。
- [ ] 出生点 2,000 半径预生成完成；BlueMap 只渲染出生点（如启用）。
- [ ] 备份文件生成；删除测试区块后恢复成功；`level.dat` 与区块数量一致。
- [ ] 重启后领地/国家/玩家数据保留。

## G. 合规

- [ ] 服务器目录不可 HTTP 下载（`curl http://host/../Earth1000/level.dat` 失败）。
- [ ] 备份目录权限 600，且不在公开桶。
- [ ] `LICENSE-NOTICES.txt` 随客户端包分发。
- [ ] 公网 BlueMap 已获书面许可，或保持关闭。
- [ ] 赞助页面不含数值/领地/战斗力。

## 记录

| 项 | 结果 | 证据 |
|---|---|---|
| 坐标 6 点 |  | 截图/日志 |
| 双端互通 |  | 截图 |
| 客户端安装 |  | 安装日志 |
| 备份恢复 |  | 文件列表 |
| TPS |  | `/tps` 输出 |
