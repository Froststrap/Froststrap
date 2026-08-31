<h1 align="center">
    Froststrap
</h1>

<p align="center">
    Froststrap is a cross-platform Roblox booststrapper which started from <a href="https://github.com/fishstrap/fishstrap.git"><strong>Fishstrap</strong></a>.
</p>

<p align="center">
    <img src="./.resources/froststrap.png" height=200 alt="logo"/>
</p>

<p align="center">
    If you'd like to support our project, consider giving this repository a star!
</p>

<div align="center">

[![License][badge-repo-license]][repo-license]
[![Downloads (Total)][badge-repo-downloads-total]][repo-releases]
[![Downloads (Latest)][badge-repo-downloads]][repo-releases]
[![Version][badge-repo-latest]][repo-latest]
[![Stars][badge-repo-stars]][repo-stargazer]
[![Discord][badge-discord]][discord-invite]

</div>

> [!CAUTION]
> The repo, [Froststrap/Froststrap](https://github.com/Froststrap/Froststrap.git), and [our website](https://froststrap.xyz), are the **ONLY PLACES** you should
> download the binary/executable from, as any other source is **NOT** affiliated with us, and is a potential threat. 

---

## Key Improvements Over Fishstrap

### Integrations
- Automatically rejoin servers you were disconnected from due to inactivity
- Disable Roblox’s built-in screenshot and video recording system
- Custom Froststrap Discord RPC that shows the current page/dialog
- Replace "Playing Roblox" with the name of the game you're playing using Custom Status Display
- The playtime counter shows both total and session playtime
- Roblox Studio RPC integrated within Froststrap
    * Change the Studio RPC thumbnail depending on the script that is open
    * Show script type, name, and number of lines of code

### Bootstrapper
- Change the Roblox process priority
- Automatically close the Roblox Crash Handler to reduce memory usage
- Integrated cleaner tool to remove leftover files

### Mods
- Multi-mod system that allows you to download many mods at once
- Select when to apply the mod (player/studio)
- Download community-made mods directly from within the app
- Generate mods using a hex code, with the option to also color the cursor, Shift Lock, or Emote Wheel
- Change the cursor, Shift Lock, death sound, and game font by selecting a file
- Use custom cursor sets to change between your cursors faster

### FastFlag Enhancements
- Automatic message when applying FastFlags that are not in the Roblox FastFlag Allowlist
- Create or use FastFlag profiles
- Change all Roblox FastFlags in the allowlist via the FastFlag settings
- Click 'Clean List' to remove flags that are not in the Roblox FastFlag Allowlist

### UI & Appearance
- Fully customizable bootstrapper launcher
- Change the app font to any font you prefer
- Supports image and gradient background themes
- Built-in app themes
- Change the window background to Aero, Acrylic, or Mica

### Settings
- Easily switch Roblox update channels
- Option to fully block Roblox updates
- Replace Roblox’s changing version-xxxxx folders with a non-changing folder
- View all currently available Roblox channels

### Extra Features
- Easily import settings from other bootstrappers such as Fishstrap and Bloxstrap
- Create game shortcuts for faster game joining
- Join servers in your region more easily using the region selector
- Join servers in your selected region through the system tray while playing
- Built-in account manager
    * Region selector

More features are planned! You can also suggest new features in the Issues section.

---

## Licensing

Froststrap uses a **multi-license model** depending on the type of code:

| Code                         | Locaiton                                                                      | License                                                   |
|------------------------------|-------------------------------------------------------------------------------|-----------------------------------------------------------|
| Upstream code from Fishstrap | `n/a` - Can be found anywhere before we touched                               | [MIT](https://opensource.org/licenses/MIT)                |
| Code written by Froststrap   | `n/a` - Can be found anywhere else                                            | [AGPL-3.0](https://opensource.org/license/agpl-3-0)       |
| Rust + F# code               | [`./backend`](./backend), [`./Scripts/Translations`](./Scripts/Translations)  | [MPL-2.0](https://opensource.org/license/MPL-2.0)         |
| Nix code                     | [`./nix`](./nix), [`./flake.nix`](flake.nix), [`./flake.lock`](./flake.lock)  | [Unlicense](https://unlicense.org/)                       |

When in doubt about which license applies to a specific file, check the file header, or refer to table above.

## Star History

### To support the development of Froststrap, consider giving the repository a star.

<a href="https://www.star-history.com/?repos=Froststrap%2FFroststrap&type=date&logscale=&legend=top-left">
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
[repo-stargazer]: https://github.com/Froststrap/Froststrap/stargazers

[discord-invite]: https://discord.gg/KdR9vpRcUN
