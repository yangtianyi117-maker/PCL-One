PCL One · 手机端说明
====================

包内只有一个 APK：

  PCLProMobile-2.1.0.apk

它是「地球服联机助手」，不是 Minecraft 启动器：
- 地球服：手机 Bedrock 124.221.231.213:19132（Geyser 转到 Java 世界）
- 卡片显示在线人数/延迟，一键拉起 Minecraft 并复制地址
- 「地球指南」按钮在 APK 内打开（assets/earth.html），没网也能看；
  想用浏览器打开时才走公网地址

它不能运行 1.7.10 泰坦整合。手机玩泰坦需要 PojavLauncher 一类的
Java-on-Android 运行时（本包未内置，避免出现第二个 APK 与额外的
GPL/签名/兼容性负担）。

安装：
1. 手机开启「USB 调试」，数据线连电脑；
2. 双击 PCL One.exe →「安装 APK 到手机」；
   或把本目录的 APK 发到手机，允许未知来源后安装。

如果 adb 不在默认路径，手动复制 APK 即可。
