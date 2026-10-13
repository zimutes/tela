# Third-party notices

Tela itself is MIT-licensed (see [LICENSE](LICENSE)). It uses these projects, each under its
own license:

| Component | Use | License |
|---|---|---|
| [mpv](https://mpv.io) (libmpv) | Video engine: decoding, rendering, subtitles, audio output | GPL-2.0-or-later, or LGPL-2.1-or-later when built without the GPL parts |
| [FFmpeg](https://ffmpeg.org) (inside libmpv) | Demuxing and decoding | LGPL-2.1-or-later or GPL-2.0-or-later, depending on the build |
| [libass](https://github.com/libass/libass) (inside libmpv) | Subtitle rendering | ISC |
| [Avalonia](https://avaloniaui.net) | User interface | MIT |
| [SkiaSharp](https://github.com/mono/SkiaSharp) / [HarfBuzzSharp](https://github.com/mono/SkiaSharp) | Drawing and text (through Avalonia) | MIT |
| [Tmds.DBus.Protocol](https://github.com/tmds/Tmds.DBus) | Keeping the screen on (Linux) | MIT |
| Barlow, Barlow Condensed, JetBrains Mono, IBM Plex Sans, IBM Plex Mono, Outfit, DM Sans, Playfair Display, Share Tech Mono (from [Google Fonts](https://github.com/google/fonts)) | Theme fonts | SIL Open Font License 1.1 |

- **libmpv on Windows** is the build by shinchiro
  ([mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake)), the one mpv's site
  points to for Windows. `tools/obter-libmpv.ps1` downloads a pinned release and checks its
  SHA-256. It is used unmodified, as a separate shared library (`libmpv-2.dll`) next to
  `Tela.exe`, loaded at run time, so it can be replaced with any other libmpv 2.x build. The
  source of mpv, FFmpeg and the other libraries in that build is linked from that project.
  Builds of libmpv that include GPL parts make the combination subject to the GPL when it is
  distributed: Tela's own code is MIT, which is compatible, and its source is in this
  repository.
- **libmpv on Linux** is not included: Tela loads the one installed by your distribution
  (`libmpv.so.2`).
- **Fonts:** the static weights were cut from the variable fonts with fontTools and their names
  normalised (the same files as in Fita); the license of each family is next to it in
  `src/Tela/Assets/Fonts/*/OFL.txt`.
