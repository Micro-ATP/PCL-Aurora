**English** | [简体中文](README-ZH_CN.md)

<div align="center">

<img src="src/PCL.Aurora.Desktop/Assets/Icons/AppIcon-2048.png" alt="PCL Aurora icon" width="96" height="96">

# Plain Craft Launcher (PCL) Aurora Port

PCL Aurora is a cross-platform port of PCL for Windows, macOS, and Linux.

[![Stars](https://img.shields.io/github/stars/Micro-ATP/PCL-Aurora?style=for-the-badge&label=Stars&labelColor=444444&color=eac54f)](https://github.com/Micro-ATP/PCL-Aurora/stargazers)
[![Release](https://img.shields.io/github/v/release/Micro-ATP/PCL-Aurora?style=for-the-badge&label=Release&logo=github)](https://github.com/Micro-ATP/PCL-Aurora/releases/latest)
[![Issues](https://img.shields.io/github/issues/Micro-ATP/PCL-Aurora?style=for-the-badge&label=Issues&labelColor=444444&color=1f883d)](https://github.com/Micro-ATP/PCL-Aurora/issues)
[![License](https://img.shields.io/badge/License-Apache--2.0%20%2B%20PCL%20Terms-1677b8?style=for-the-badge)](LICENSE)

[Releases](https://github.com/Micro-ATP/PCL-Aurora/releases) |
[Report an issue](https://github.com/Micro-ATP/PCL-Aurora/issues/new/choose) |
[Original PCL](https://github.com/Meloong-Git/PCL) |
[PCL Community Edition](https://github.com/PCL-Community/PCL-CE)

</div>

> [!IMPORTANT]
> PCL Aurora is an independently developed and maintained third-party port. It does not share the official maintenance roadmap of PCL or PCL-CE. Please report Aurora issues in this repository, not to the PCL or PCL-CE maintainers.

## About the project

PCL Aurora is not a launcher built from scratch. It is based on [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) and [PCL Community Edition](https://github.com/PCL-Community/PCL-CE), carrying their mature interface, interaction patterns, and Minecraft management features to a cross-platform codebase. Avalonia and .NET are used to rebuild platform-specific boundaries so that the same experience can progressively run on Windows, macOS, and Linux.

Development currently prioritizes finalizing the macOS interface and core workflows before consolidating Windows and Linux support. The project remains under active development and should not yet be treated as a stable replacement for the original PCL.

## Current capabilities

- Launch and accounts: instance discovery, multiple offline accounts, Microsoft authentication flow, Java detection, launch preparation, and game process management.
- Game downloads: release, snapshot, legacy, and special version catalogs; isolated installation directories; file verification; staged progress; transfer speed; and task cancellation.
- Loaders and community content: Forge, NeoForge, Fabric, OptiFine, and other loader catalogs, plus search, favorites, and downloads for mods, modpacks, resource packs, shaders, data packs, and worlds.
- Instance management: version selection, isolation settings, folder actions, content summaries, mod management, and update checks.
- Launcher settings: launch, Java, personalization, language, miscellaneous, update, feedback, and log pages, including light and dark themes and system font selection.
- Additional tools: built-in help, cross-platform file downloads, skin and achievement generation, junk cleanup, and memory optimization.
- Local data protection: preferences, favorites, and logs remain local; Microsoft refresh tokens are stored in the operating system's protected credential store.

Some controls vary with platform capabilities. Multiplayer tooling is still being designed. Microsoft authentication is implemented, but public builds may receive an HTTP `403` response until the Minecraft Services application review is complete.

## Platform support

| Platform | Current status | Notes |
|---|---|---|
| macOS | Primary development and validation platform | Core UI, downloads, instance management, and launch workflows are being finalized here |
| Windows | Porting target | Platform-specific features and release packaging still require system-level validation |
| Linux | Porting target | Runtime and packaging work for Arch Linux and other distributions will follow macOS stabilization |

Only systems and architectures explicitly listed on a release page are supported by that release. Successful source compilation does not imply that every platform feature is complete.

## Install and run

Most users should obtain published builds from [Releases](https://github.com/Micro-ATP/PCL-Aurora/releases). During development, the launcher can also be run from source.

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Network access to Minecraft, Microsoft, GitHub, Modrinth, CurseForge, and the selected download mirrors
- A Java runtime compatible with the target Minecraft version; the launcher can assist with installation on supported platforms

### Run from source

```bash
git clone https://github.com/Micro-ATP/PCL-Aurora.git
cd PCL-Aurora
dotnet run --project src/PCL.Aurora.Desktop/PCL.Aurora.Desktop.csproj
```

Build the Release configuration with:

```bash
dotnet build src/PCL.Aurora.Desktop/PCL.Aurora.Desktop.csproj -c Release
```

## Feedback and contributions

Before filing an issue, confirm that you are using the latest source or release. Include the operating system, processor architecture, reproduction steps, and launcher logs whenever possible. Remove Microsoft tokens, account credentials, and personal paths before sharing diagnostics.

- [Report an issue](https://github.com/Micro-ATP/PCL-Aurora/issues/new/choose)
- [Browse existing issues](https://github.com/Micro-ATP/PCL-Aurora/issues)
- [View contributors](https://github.com/Micro-ATP/PCL-Aurora/graphs/contributors)

## License

This repository follows a mixed-license structure that reflects the origin of its contents:

- Original PCL Aurora contributions copyrighted by Micro-ATP are licensed under the [Apache License 2.0](LICENSE).
- Code, interface designs, and resources adapted or directly reused from PCL or PCL-CE remain subject to the applicable PCL Limited Distribution License and PCL Repository Fair Use Guidelines.
- Third-party components, fonts, icons, and resources remain under their respective licenses.

The root license does not relicense upstream or third-party material. See [NOTICE](NOTICE) and [LICENSES](LICENSES) for source mappings, modification notes, and complete license texts. Before copying, modifying, or distributing any part of the project, identify and comply with every term applicable to that material.

## Sources and acknowledgements

- [Plain Craft Launcher](https://github.com/Meloong-Git/PCL), created by LTCat (龙腾猫跃). PCL Aurora is an independent, substantial derivative work based on PCL. Please support the original author through [Afdian](https://meloong.com/afd/a/LTCat).
- [PCL Community Edition](https://github.com/PCL-Community/PCL-CE), which provides many community improvements and valuable references for features, interface behavior, and this port.
- [Avalonia](https://github.com/AvaloniaUI/Avalonia), the cross-platform desktop UI framework used by Aurora.
- BMCLAPI, Modrinth, CurseForge, MC Encyclopedia, and the other projects listed in this repository provide download, metadata, and localization support.

This project is not an official Minecraft product. It is not approved by or affiliated with Mojang Studios or Microsoft.

## Contributors

<a href="https://github.com/Micro-ATP/PCL-Aurora/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=Micro-ATP/PCL-Aurora" alt="PCL Aurora contributors">
</a>
