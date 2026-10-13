<p align="center">
  <img src="docs/icon.png" width="96" alt="Tela icon: a screen with a play triangle">
</p>

<h1 align="center">Tela</h1>

<p align="center">
  <b>All you need. Nothing else.</b><br>
  A video player for Windows and Linux.<br>
  <sub>by zimutek</sub>
</p>

<p align="center">
  <a href="LEIAME.md">Leia-me em português</a> · Windows 10 / 11 · Linux · .NET 10 · libmpv · MIT
</p>

<p align="center">
  <a href="https://github.com/zimutes/tela/actions/workflows/compilar.yml"><img src="https://github.com/zimutes/tela/actions/workflows/compilar.yml/badge.svg" alt="Build"></a>
</p>

<p align="center">
  <img src="docs/themes.png" width="900" alt="Tela playing a video in its six themes: Purple Haze, 1995 Calling, Midnight OLED, Boutique Gold, Toxic Terminal and Oceanic Flow">
</p>

---

> ### Read this first
>
> **Written by AI.** This app (the code, the icon and this README) was written by AI
> (Claude, by Anthropic, through Claude Code) at the author's request, and is shared as-is.
> It was tested on the author's Windows 11 PC (hardware decoding through D3D11) and on
> Fedora 44 in a container, without a graphics card (software rendering), with H.264, HEVC,
> MPEG-4, AAC, MP3, embedded and external subtitles, several audio tracks and chapters.
>
> **No warranty of any kind**, see [LICENSE](LICENSE).

---

## Why

VLC plays everything and shows you everything: menus inside menus, toolbars, settings you
will never touch. Tela (Portuguese for *screen*, and the canvas a film is projected on) plays
everything too, through mpv's engine, and then gets out of the way: the picture fills the
window, the controls float on top and fade when you watch, and the things you actually need
are one key or one click away. Same family as [Fita](https://github.com/zimutes/fita), the
music player: same six themes, same "by zimutek".

<p align="center">
  <img src="docs/home.png" width="720" alt="The start screen: open a file or a folder, and the videos you left halfway, with a picture and how much is left">
</p>

## What it does

- **Plays almost everything**, through libmpv (mpv's engine) and FFmpeg: MKV, MP4, MOV, AVI,
  WebM, WMV, FLV, MPEG-TS, VOB and more; H.264, HEVC, AV1, VP9, MPEG-2/4; HDR is tone-mapped.
  **Hardware decoding** when the graphics card can, software when it can't.
- **Picks up where you left off.** Every video remembers its position, the audio and subtitle
  tracks you chose and the delays you set. The start screen shows *Continue watching*, with a
  picture of where you stopped and how much is left.
- **Series-aware.** Opening one episode brings its folder along, in natural order (E2 before
  E10). Near the end a card counts down to the next episode (*Play now* / *Cancel*). The
  languages you pick for audio and subtitles carry over to the next episode.
- **Subtitles done right:** the matching `.srt` next to the video (or in a `Subs` folder) loads
  by itself; embedded tracks, forced tracks, a preferred language, size, a dark box behind for
  bright scenes, delay in 100 ms steps, and drag a subtitle file onto the window to add it.
  Subtitles move up when the controls appear, so they are never hidden behind them.
- **The picture is the window.** It opens at the video's shape (no black bars); the controls
  float on top and fade with the cursor while it plays; double-click for full screen; drag the
  picture to move the window; a mini player that stays on top in a corner (`I`).
- **Seek bar with previews:** hover to see a thumbnail of that moment and the chapter's name;
  chapters cut the bar into segments; dragging scrubs, letting go lands exactly.
- **Everything essential, nothing more:** speed (0.25× to 4×, with pitch kept), frame by frame,
  A–B loop, repeat, aspect ratio, crop to fill, rotate, deinterlace, screenshots (with or
  without subtitles), audio output device, even-out volume (for films where whispers and
  explosions live 30 dB apart), volume up to 150%, media info.
- **Keeps the screen on** while a video plays (and only then).
- **Keyboard and mouse:** keys from mpv, VLC and YouTube (below); wheel for volume, the side
  buttons of the mouse for ±10 s, right-click for the full menu, middle-click to mute.
- **Six themes**, the same as Fita's (from the panel of Jazmin, the author's CMS): Purple Haze,
  1995 Calling, Midnight OLED, Boutique Gold, Toxic Terminal and Oceanic Flow. They change
  fonts, shapes and relief, not just colours.
- **One instance:** opening a file with Tela already open plays it there. On Windows, *Open
  with* for video files, and right-click on a folder gives *Watch in Tela*.
- **Works without a graphics card too:** if there is no OpenGL (virtual machines, remote
  desktops, broken drivers) the video is drawn in software, by itself. If the picture ever
  stays black, *Settings → Draw video in software* does the same on purpose.
- **English or Portuguese.**

### Keys

| Keys | |
|---|---|
| `Space` · `K` · click | Play / pause |
| `←` `→` · `J` `L` | Back / forward 5 s · 10 s |
| `Shift+←` `→` · `,` `.` | 1 s · one frame at a time |
| `0`–`9` · `Home` | Jump to 0–90% · to the start |
| `PgUp` `PgDn` | Previous / next chapter |
| `↑` `↓` · `M` · wheel | Volume · mute |
| `F` · double-click · `Esc` | Full screen · leave |
| `I` · `T` | Mini player · always on top |
| `N` · `P` | Next / previous video in the folder |
| `V` · `B` | Next subtitles · next audio track |
| `Z` `X` · `Shift+Z` `X` | Subtitle delay · audio delay (−/+ 100 ms) |
| `G` `H` | Smaller / bigger subtitles |
| `[` `]` · `Backspace` | Slower / faster · normal speed |
| `R` · `Shift+R` | Loop a part (A, B, off) · repeat the video |
| `A` · `C` · `D` | Aspect ratio · crop to fill · deinterlace |
| `S` · `Shift+S` | Screenshot · without subtitles |
| `Shift+I` | Media info |
| `Ctrl+O` · `Ctrl+Shift+O` · `Ctrl+V` | Open file · folder · paste a copied file or path |
| `Ctrl+W` · `Ctrl+Q` · `F1` | Close the video · quit · all the keys |

## What you need

- **Windows 10 or 11 (x64)**: nothing else. libmpv comes with Tela (`libmpv-2.dll`, next to
  `Tela.exe`).
- **Linux (x64 or arm64)** with X11 or XWayland, and **libmpv** from your distribution:
  `sudo dnf install mpv-libs` (Fedora), `sudo apt install libmpv2` (Debian/Ubuntu),
  `sudo pacman -S mpv` (Arch). On Fedora, the default `ffmpeg-free` plays H.264 (through
  OpenH264) but not HEVC (H.265): for that, swap in RPM Fusion's FFmpeg
  (`sudo dnf swap ffmpeg-free ffmpeg --allowerasing`). Tela tells you when a video's format
  can't be decoded, instead of showing a black picture.
- To build: the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

## Install

**Windows** (PowerShell):

```powershell
git clone https://github.com/zimutes/tela
cd tela
.\tools\publicar.ps1              # downloads libmpv, builds and installs for the current user
.\tools\publicar.ps1 -Autonomo    # self-contained, no .NET runtime needed
.\tools\publicar.ps1 -SemMenus    # without the right-click entry on folders
```

It installs to `%LOCALAPPDATA%\Programs\Tela`, adds **Tela** to the Start menu and to *Open
with* for video files (and to Windows' *Default apps*, without becoming the default), and
(unless `-SemMenus`) adds *Watch in Tela* to the right-click menu of folders. No admin rights
needed. To remove it: `.\tools\desinstalar.ps1` (`-Tudo` also deletes the settings and the
history, `-WhatIf` shows what it would do).

**Linux**:

```sh
git clone https://github.com/zimutes/tela
cd tela
./tools/instalar.sh               # builds and installs for the current user
./tools/instalar.sh --remover     # removes it
```

It installs to `~/.local/opt/tela`, with a `tela` command in `~/.local/bin` and an entry in
your applications menu (with *Open with* for video files), and tells you if libmpv is missing.

Releases carry a small `Tela-win-x64.zip` (needs the .NET 10 runtime), a self-contained
Windows zip and self-contained Linux packages (`./tela/instalar.sh` installs the one that
comes with it).

## How it works

- **Engine:** libmpv, through a small binding of its C API of our own (`src/Tela/Mpv`). A
  thread waits for mpv's events; property changes reach the interface on its thread; the
  position, which changes every frame, is read when needed instead of flooding it.
- **Picture:** mpv draws through its OpenGL render API straight into the framebuffer Avalonia
  gives the video control, so the rest of the interface is drawn on top with no native
  "holes": that is what lets the controls, the preview and the messages float over the
  picture. Without OpenGL, mpv's software render API draws each frame into one of three
  bitmaps on its own thread.
- **Thumbnails:** a second, silent libmpv (no audio, no subtitles, no GPU) seeks to the nearest
  keyframe and draws a small frame in memory, for the seek bar and for *Continue watching*.
- **Interface:** Avalonia, with the controls that matter drawn by hand so the themes can
  change their shape, not just their colour.

## Security

Tela opens files it didn't make, through mpv and FFmpeg, so:

- **No user config, no scripts, no youtube-dl.** It never reads an `mpv.conf` or runs Lua
  scripts that may be installed for mpv, and never fetches anything from the internet.
- **Local files only.** Formats that open other files from inside (HLS, concat) may only open
  local files, never URLs; playlists inside files are not followed (open the videos or their
  folder instead). A file disguised as a video is refused.
- **Single instance, current user only:** the pipe that passes files to the running window
  only accepts connections from the same user.
- No network access, no telemetry, no admin rights. Settings and the history live in
  `%APPDATA%\Tela` (Windows) or `~/.config/tela` (Linux), and errors in `log.txt` there.

## Build

```sh
dotnet build Tela.slnx -c Release
dotnet test tests/Tela.Tests
```

On Windows, `tools/obter-libmpv.ps1` downloads the pinned libmpv (checked against its SHA-256)
into `lib/win-x64/`; the build copies it next to `Tela.exe`.

- `src/Tela/Mpv/`: the libmpv binding (`Native`), the player and its events (`MpvPlayer`),
  thumbnails (`Thumbnailer`).
- `src/Tela/Controls/`: the video (`VideoView` in OpenGL, `SoftwareVideoView` without), the
  hand-drawn sliders, icons and backgrounds.
- `src/Tela/Library/`: which files open and in what order (`MediaFiles`), the list
  (`PlayQueue`), where each video stopped (`History`).
- `src/Tela/Views/`: the window, split by area (player, window and keys, overlay, menus, start screen).
- `src/Tela/Loc.cs`: every piece of UI text, in English and Portuguese.
- `Tela --selftest file…` runs files through libmpv without a window (opens, plays, seeks, reads
  tracks and chapters, makes a thumbnail).
- `tools/Capturas` draws the window in every theme without a screen (the pictures above).
- A tag `v*` makes GitHub Actions publish a release (Windows and Linux) with `SHA256SUMS`.

## License

[MIT](LICENSE) © zimutek. Third-party parts (libmpv, FFmpeg, Avalonia, the fonts) keep their
own licenses: see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
