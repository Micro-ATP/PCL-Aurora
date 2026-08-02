[English](README.md) | **简体中文**

<div align="center">

<img src="src/PCL.Aurora.Desktop/Assets/Icons/AppIcon-2048.png" alt="PCL Aurora 图标" width="96" height="96">

# Plain Craft Launcher (PCL) Aurora Port

PCL Aurora 是面向 Windows、macOS 与 Linux 的 PCL 跨平台迁移项目。

[![Stars](https://img.shields.io/github/stars/Micro-ATP/PCL-Aurora?style=for-the-badge&label=Stars&labelColor=444444&color=eac54f)](https://github.com/Micro-ATP/PCL-Aurora/stargazers)
[![Release](https://img.shields.io/github/v/release/Micro-ATP/PCL-Aurora?style=for-the-badge&label=Release&logo=github)](https://github.com/Micro-ATP/PCL-Aurora/releases/latest)
[![Issues](https://img.shields.io/github/issues/Micro-ATP/PCL-Aurora?style=for-the-badge&label=Issues&labelColor=444444&color=1f883d)](https://github.com/Micro-ATP/PCL-Aurora/issues)
[![License](https://img.shields.io/badge/License-Apache--2.0%20%2B%20PCL%20Terms-1677b8?style=for-the-badge)](LICENSE)

[查看发行版](https://github.com/Micro-ATP/PCL-Aurora/releases) |
[提交问题](https://github.com/Micro-ATP/PCL-Aurora/issues/new/choose) |
[原版 PCL](https://github.com/Meloong-Git/PCL) |
[PCL Community Edition](https://github.com/PCL-Community/PCL-CE)

</div>

> [!IMPORTANT]
> PCL Aurora 是由 Micro-ATP 独立开发和维护的第三方跨平台迁移版本，与 PCL、PCL-CE 的官方维护路线不同。使用中遇到的问题请提交至本仓库，不要向 PCL 或 PCL-CE 仓库反馈。

## 项目简介

PCL Aurora 并不是另起炉灶的启动器。项目以 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) 与 [PCL Community Edition](https://github.com/PCL-Community/PCL-CE) 为基础，迁移其成熟的界面、交互与 Minecraft 管理能力，并以 Avalonia 和 .NET 重建平台相关边界，使同一套使用体验能够逐步运行在 Windows、macOS 与 Linux 上。

当前开发优先完成 macOS 版本的界面与核心流程定型，再继续收敛 Windows 和 Linux 适配。项目仍处于积极开发阶段，不建议将其视为原版 PCL 的稳定替代品。

## 已有能力

- 启动与账户：实例识别、离线多账户、Microsoft 登录流程、Java 检测、启动参数准备与游戏进程管理。
- 游戏下载：正式版、预览版、远古版与特殊版本目录，独立安装目录，文件校验、分阶段进度、速度显示与任务取消。
- 加载器与资源：Forge、NeoForge、Fabric、OptiFine 等加载器目录，以及 Mod、整合包、资源包、光影、数据包和世界资源的检索、收藏与下载。
- 实例管理：版本选择、隔离设置、文件夹操作、内容统计、Mod 管理与更新检查。
- 启动器设置：启动、Java、个性化、语言、杂项、更新、反馈和日志等页面，支持亮暗主题与系统字体选择。
- 更多工具：内置帮助、跨平台文件下载、皮肤与成就生成、垃圾清理、内存优化等百宝箱能力。
- 本机数据保护：偏好、收藏与日志保存在本机；Microsoft 刷新令牌交由系统安全凭据库保存。

部分入口会随当前平台能力而调整。联机功能仍在设计中；Microsoft 正版登录代码已接入，但在 Minecraft Services 完成应用审核前，公开构建可能收到服务端 `403` 拒绝。

## 平台支持

| 平台 | 当前状态 | 说明 |
|---|---|---|
| macOS | 主要开发与验证平台 | 核心界面、下载、实例管理和启动链路正在此平台定型 |
| Windows | 迁移目标 | 平台专属能力与发行流程仍需系统验证 |
| Linux | 迁移目标 | Arch Linux 等发行版的运行与打包适配将在 macOS 定型后推进 |

只有发布页面明确列出的系统与架构才属于对应版本的正式支持范围。源码能够编译不等于所有平台功能已经完成。

## 获取与运行

普通用户应优先从 [Releases](https://github.com/Micro-ATP/PCL-Aurora/releases) 获取已经发布的构建。开发阶段也可以从源码运行。

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- 可访问 Minecraft、Microsoft、GitHub、Modrinth、CurseForge 与所选下载镜像的网络环境
- 启动游戏时需要与目标 Minecraft 版本匹配的 Java；启动器也可以在受支持平台上协助安装

### 从源码运行

```bash
git clone https://github.com/Micro-ATP/PCL-Aurora.git
cd PCL-Aurora
dotnet run --project src/PCL.Aurora.Desktop/PCL.Aurora.Desktop.csproj
```

构建 Release 版本：

```bash
dotnet build src/PCL.Aurora.Desktop/PCL.Aurora.Desktop.csproj -c Release
```

## 反馈与贡献

提交问题前，请确认使用的是最新代码或最新发行版，并尽量附上操作系统、处理器架构、复现步骤和启动器日志。安全信息、Microsoft 令牌、账户凭据与个人路径请先脱敏。

- [报告问题](https://github.com/Micro-ATP/PCL-Aurora/issues/new/choose)
- [查看现有问题](https://github.com/Micro-ATP/PCL-Aurora/issues)
- [查看贡献者](https://github.com/Micro-ATP/PCL-Aurora/graphs/contributors)

## 许可证

本仓库采用与其来源结构相符的混合许可：

- Micro-ATP 拥有版权的 PCL Aurora 原创贡献采用 [Apache License 2.0](LICENSE)。
- 从 PCL、PCL-CE 迁移、改编或直接复用的代码、界面与资源，继续受对应的《PCL 分发有限许可》和《PCL 存储库合理使用指南》约束。
- 第三方组件、字体、图标与资源按各自许可证提供。

根许可证不会重新授权任何上游或第三方内容。具体来源、修改说明和完整许可文本见 [NOTICE](NOTICE) 与 [LICENSES](LICENSES)。复制、修改或分发前，请同时确认并遵守所有适用于目标内容的条款。

## 来源与致谢

- [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)，作者龙腾猫跃。PCL Aurora 属于基于 PCL 的第三方重度二次创作；请通过[爱发电](https://meloong.com/afd/a/LTCat)支持原作者。
- [PCL Community Edition](https://github.com/PCL-Community/PCL-CE)，提供了大量经过社区改进的功能、界面与跨平台迁移参考。
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)，提供跨平台桌面 UI 框架。
- BMCLAPI、Modrinth、CurseForge、MC 百科及仓库中列明的其他项目，为下载、资源信息和本地化能力提供支持。

本项目不是 Minecraft 官方产品，未经 Mojang Studios 或 Microsoft 批准，也不与其存在从属或官方合作关系。

## 贡献者

<a href="https://github.com/Micro-ATP/PCL-Aurora/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=Micro-ATP/PCL-Aurora" alt="PCL Aurora 贡献者">
</a>
