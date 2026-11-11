<h1 align="center">
    Froststrap
</h1>

<p align="center">
    Froststrap is a cross-platform Roblox bootstrapper derived from <a href="https://github.com/fishstrap/fishstrap.git"><strong>Fishstrap</strong></a>.
</p>

<p align="center">
    <img src="./.resources/froststrap.webp" height="200" alt="Froststrap logo"/>
</p>

<div align="center">

[![License][badge-repo-license]][repo-license]
[![Downloads (Total)][badge-repo-downloads-total]][repo-releases]
[![Downloads (Latest)][badge-repo-downloads]][repo-releases]
[![Version][badge-repo-latest]][repo-latest]
[![Stars][badge-repo-stars]][repo-stargazer]
[![Discord][badge-discord]][discord-invite]

</div>

## Download

> [!NOTE]
> There's no `.exe` installer for Windows. Download the `.msi` instead.

Choose an installation method for your operating system:

| System | Download |
| :- | :- |
| Windows | [**Installer (.msi)**][download-windows], or run `winget install Froststrap.Froststrap` |
| macOS (Apple Silicon) | [**Installer (.pkg)**][download-macos-arm64] |
| macOS (Intel) | [**Installer (.pkg)**][download-macos-x64] |
| Debian, Ubuntu, Linux Mint, Pop!_OS | [**.deb**][download-linux-deb] |
| Fedora, RHEL, Nobara, Bazzite, Rocky, openSUSE | [**.rpm**][download-linux-rpm] |
| Arch, Omarchy, CachyOS, EndeavourOS | [**AUR**][download-linux-aur]: `yay -S froststrap-bin` |
| Universal Linux | [**.AppImage**][download-linux-appimage] |
| Nix | `nix run github:Froststrap/Froststrap` |

All release files are also listed under **Assets** on the [latest release][repo-latest] page.

> [!CAUTION]
> Download Froststrap binaries and executables **only** from the official repository, [Froststrap/Froststrap](https://github.com/Froststrap/Froststrap.git), or [our website](https://froststrap.xyz). Other download sources are **not affiliated** with the project and may pose a security risk.

## Platform Support

### Tier I

Tier I platforms are guaranteed to work, and we provide binaries for them.

| System | Notes |
| :- | :- |
| Windows | Windows 10 LTSC or Windows 11 and later |
| macOS aarch64 >= 26.0 | `aarch64` is also known as `arm64`. Requires macOS 26 (Tahoe) or later |
| NixOS | Binaries run through our wrapper; as long as you use it, everything works correctly |

### Tier II

Tier II platforms are supported, and we ship binaries for them. However, support may be less reliable due to reduced reproducibility or a higher maintenance burden.

| System | Notes |
| :- | :- |
| Arch Linux | Stability is not guaranteed. We will attempt to reproduce reported issues but generally assume they are specific to Arch Linux |
| Other macOS | Macs not covered above (such as Intel Macs or macOS older than 26) aren't a focus, but we may look at specific issues case by case |
| Fedora | Dependency lists have not been tested, so additional packages may be required and compatibility is not guaranteed |
| Ubuntu/Debian | Dependency lists have not been tested, so additional packages may be required and compatibility is not guaranteed |

### Tier III

| System | Notes |
| :- | :- |
| Anything not listed | We will try to help with reported issues, but distribution-specific problems or limitations may be declined or given lower priority than other work |

## Key Improvements Over Fishstrap

### Integrations
- Automatically rejoin servers after being disconnected due to inactivity
- Disable Roblox's built-in screenshot and video recording system
- Show which Froststrap page or dialog you're on with Froststrap's own Discord RPC
- Replace "Playing Roblox" with the current game's name using Custom Status Display
- Track both total and session playtime with the playtime counter
- Show your Roblox Studio activity on Discord with the built-in Studio RPC
    * Change the Studio RPC thumbnail based on the currently open script
    * Display the script's type, name, and line count

### Mods
- Download multiple mods at once using the multi-mod system
- Choose whether to apply a mod to Roblox Player or Roblox Studio
- Download community-made mods directly in the app
- Generate mods using hexadecimal color codes to customize the cursor, Shift Lock, or Emote Wheel
- Customize the cursor, Shift Lock, death sound, and game font by selecting files
- Switch between cursors more quickly using custom cursor sets

### FastFlag Enhancements
- Receive automatic notifications when applying FastFlags that are not on the Roblox FastFlag allowlist
- Create and use FastFlag profiles
- Change all allowlisted Roblox FastFlags through the FastFlag settings
- Click **Clean List** to remove flags that are not on the Roblox FastFlag allowlist

### UI & Appearance
- Fully customize the bootstrapper launcher
- Change the application font to one you prefer
- Use image or gradient background themes
- Choose from built-in application themes
- Change the window backdrop to Aero, Acrylic, or Mica

### Settings
- Switch between Roblox update channels easily
- Block Roblox updates entirely
- Replace Roblox's changing `version-xxxxx` folders with a fixed folder
- View all currently available Roblox channels

### Extra Features
- Import settings from other bootstrappers, such as Fishstrap and Bloxstrap
- Create game shortcuts to join games more quickly
- Use the region selector to join servers in your region more easily
- Join servers in your selected region through the system tray while playing
- Manage accounts using the built-in account manager

More features are planned! You can suggest new ones by opening a GitHub issue.

---

## Licensing

All new code revisions are licensed under the [Mozilla Public License 2.0 (MPL-2.0)](https://opensource.org/license/MPL-2.0).

## Stargazing

Consider starring the repository to support Froststrap's development.

<a href="https://www.star-history.com/?repos=Froststrap%2FFroststrap&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=Froststrap/Froststrap&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=Froststrap/Froststrap&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=Froststrap/Froststrap&type=date&legend=top-left" />
 </picture>
</a>

<!-- Badge defs -->
[badge-repo-license]: https://img.shields.io/github/license/Froststrap/Froststrap?style=for-the-badge&color=37add9
[badge-repo-downloads]: https://img.shields.io/github/downloads/Froststrap/Froststrap/latest/total?style=for-the-badge&color=37add9
[badge-repo-downloads-total]: https://img.shields.io/github/downloads/Froststrap/Froststrap/total?style=for-the-badge&color=37add9
[badge-repo-latest]: https://img.shields.io/github/v/release/Froststrap/Froststrap?style=for-the-badge&color=37add9
[badge-repo-stars]: https://img.shields.io/github/stars/Froststrap/Froststrap?style=for-the-badge&color=37add9
[badge-discord]: https://img.shields.io/discord/1364660238963179520?style=for-the-badge&label=discord&color=5865f2

[repo-license]: https://github.com/Froststrap/Froststrap/blob/main/LICENSE
[repo-actions]: https://github.com/Froststrap/Froststrap/actions
[repo-releases]: https://github.com/Froststrap/Froststrap/releases
[repo-latest]: https://github.com/Froststrap/Froststrap/releases/latest
[download-windows]: https://github.com/Froststrap/Froststrap/releases/latest/download/Froststrap-windows-x64.msi
[download-macos-arm64]: https://github.com/Froststrap/Froststrap/releases/latest/download/Froststrap-macos-arm64-Setup.pkg
[download-macos-x64]: https://github.com/Froststrap/Froststrap/releases/latest/download/Froststrap-macos-x64-Setup.pkg
[download-linux-aur]: https://aur.archlinux.org/packages/froststrap-bin
[download-linux-appimage]: https://github.com/Froststrap/Froststrap/releases/latest/download/Froststrap-linux-x64.AppImage
[download-linux-deb]: https://github.com/Froststrap/Froststrap/releases/latest/download/Froststrap-linux-x64.deb
[download-linux-rpm]: https://github.com/Froststrap/Froststrap/releases/latest/download/Froststrap-linux-x64.rpm
[repo-stargazer]: https://github.com/Froststrap/Froststrap/stargazers

[discord-invite]: https://discord.gg/KdR9vpRcUN
