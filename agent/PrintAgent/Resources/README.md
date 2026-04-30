# Resources

This folder contains binary assets shipped with PrintAgent.

## SumatraPDF.exe

The repo ships a vendored copy of SumatraPDF (currently **3.6.1**, ~19 MB, portable 64-bit) so the build is reproducible without any external download. The binary is included as an `<EmbeddedResource>` in `PrintAgent.csproj` and extracted to `%APPDATA%\PrintAgent\bin\` on first run.

To bump the version: download a newer portable 64-bit build from <https://www.sumatrapdfreader.org/download-free-pdf-viewer>, replace this file, and update the version line in `SUMATRAPDF-NOTICE.txt`.

SumatraPDF is GPL-3.0+ — see `SUMATRAPDF-NOTICE.txt` for the attribution and the link to the source code.

## icon.ico

Application icon used for the executable, the tray, and Windows installer entries.
