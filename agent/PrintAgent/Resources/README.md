# Resources

This folder contains binary assets shipped with PrintAgent.

## SumatraPDF.exe

The repo ships a vendored copy of SumatraPDF (currently **3.6.1**, ~19 MB, portable 64-bit) so the build is reproducible without any external download. The binary is included as an `<EmbeddedResource>` in `PrintAgent.csproj` and extracted to `%APPDATA%\PrintAgent\bin\` on first run.

To bump the version: download a newer portable 64-bit build from <https://www.sumatrapdfreader.org/download-free-pdf-viewer>, replace this file, and update the version line in `SUMATRAPDF-NOTICE.txt`.

SumatraPDF is GPL-3.0+ — see `SUMATRAPDF-NOTICE.txt` for the attribution and the link to the source code.

## icon.svg / icon.ico

`icon.svg` is the source of truth for the application icon. It's derived from [`line-md:cloud-alt-print-twotone`](https://icon-sets.iconify.design/line-md/cloud-alt-print-twotone/) by [cyberalien/line-md](https://github.com/cyberalien/line-md) (MIT license), with the SMIL animations frozen at their final state and stroke recolored to the project accent `#3b82f6`. Edit this file when you want to change the design.

`icon.ico` is the multi-size compiled bundle (16, 20, 24, 32, 40, 48, 64, 128, 256 px) consumed by:

- `<ApplicationIcon>` in `PrintAgent.csproj` — appears in Explorer, Alt-Tab, and the Velopack-generated `Setup.exe` / Add-Remove Programs entry.
- The system tray (`TrayIconHost`) which loads it as an `<EmbeddedResource>` at `LogicalName=icon.ico`.

To regenerate the ICO after editing the SVG:

```sh
cd installer
./build-icon.ps1
```

The script requires [ImageMagick](https://imagemagick.org/) on PATH (the `magick` CLI). It rasterizes the SVG into 9 PNGs in a temp folder and assembles them into the ICO. Both `icon.svg` and `icon.ico` are committed so casual contributors don't need to regenerate.
