# Resources

This folder contains binary assets shipped with PrintAgent.

## SumatraPDF.exe

The repo ships a vendored copy of SumatraPDF (currently **3.6.1**, ~19 MB, portable 64-bit) so the build is reproducible without any external download. The binary is a `<None ... CopyToOutputDirectory>` item in `PrintAgent.csproj`, so it lands next to `PrintAgent.exe` in the publish folder and Velopack packages it as-is.

It is deliberately **not** an `<EmbeddedResource>` extracted at runtime (that was the pre-0.1.5 design). Writing a PE to disk and spawning it is the "dropper" pattern that trips Defender's ML heuristics on unsigned builds, and shipping the file directly also preserves SumatraPDF's own Authenticode signature.

To bump the version: download a newer portable 64-bit build from <https://www.sumatrapdfreader.org/download-free-pdf-viewer>, replace this file, and update the version line in `SUMATRAPDF-NOTICE.txt`.

SumatraPDF is GPL-3.0+ — see `SUMATRAPDF-NOTICE.txt` for the attribution and the link to the source code.

## icon.svg / icon.ico

`icon.svg` is the source of truth for the application icon. It's [`noto:printer`](https://icon-sets.iconify.design/noto/printer/) — Google's Noto Emoji rendition of the printer glyph (U+1F5A8), Apache-2.0 licensed. Edit this file when you want to change the design.

`icon.ico` is the multi-size compiled bundle (16, 20, 24, 32, 40, 48, 64, 128, 256 px) consumed by:

- `<ApplicationIcon>` in `PrintAgent.csproj` — appears in Explorer, Alt-Tab, and the Velopack-generated `Setup.exe` / Add-Remove Programs entry.
- The system tray (`TrayIconHost`) which loads it as an `<EmbeddedResource>` at `LogicalName=icon.ico`.

To regenerate the ICO after editing the SVG:

```sh
cd installer
./build-icon.ps1
```

The script requires [ImageMagick](https://imagemagick.org/) on PATH (the `magick` CLI). It rasterizes the SVG into 9 PNGs in a temp folder and assembles them into the ICO. Both `icon.svg` and `icon.ico` are committed so casual contributors don't need to regenerate.
