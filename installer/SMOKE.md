# PrintAgent -- Manual Smoke Test Checklist

Run after each release build before tagging.

## Prerequisites

- [ ] `SumatraPDF.exe` placed at `build/agent/PrintAgent/Resources/SumatraPDF.exe`
- [ ] `build.ps1` produced the following files in `installer/Output/`:
  - `PrintAgentSetup.exe` -- the installer (Velopack Setup bootstrapper)
  - `RELEASES` -- Velopack release feed (used for auto-update)
  - `PrintAgent-X.Y.Z-full.nupkg` -- full update package

## Fresh install

- [ ] Run `PrintAgentSetup.exe` on a clean Windows machine (no UAC elevation required -- installs per-user under `%LOCALAPPDATA%\PrintAgent`)
- [ ] First launch after install: UAC prompt for trusted root cert appears -> Accept
- [ ] PrintAgent icon appears in the tray
- [ ] `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\PrintAgent.lnk` exists (created by the OnFirstRun Velopack hook)
- [ ] `%APPDATA%\PrintAgent\logs\printagent-YYYY-MM-DD.log` exists
- [ ] `%APPDATA%\PrintAgent\printagent.pfx` exists
- [ ] Open `https://app.example.com` (or local test page) using the standalone client
- [ ] Pairing prompt appears with the correct origin
- [ ] Click "Autoriser" -> origin saved to `config.json`
- [ ] `getLocalPrinters()` returns >=1 printer (Microsoft Print to PDF)
- [ ] `print({printerName: "Microsoft Print to PDF", pdfBase64: "..."})` produces a file in the user's Documents folder
- [ ] WebSocket events `job.statusChanged` arrive in order: Submitted, Printing, Completed
- [ ] Disconnect & reconnect -> origin remembered, no new prompt

## Refusal flow

- [ ] Visit a different test origin
- [ ] Click "Refuser" -> WebSocket closes with OriginNotAuthorized
- [ ] Reconnect within 5 minutes -> automatically refused without prompt
- [ ] Wait 5 minutes -> prompt appears again

## Restart

- [ ] Reboot Windows -> PrintAgent starts automatically (Startup shortcut placed by OnFirstRun hook)
- [ ] Bound port preserved (`config.json.lastBoundPort`)

## Uninstall

- [ ] Apps & features -> Uninstall PrintAgent (or run `PrintAgentSetup.exe --uninstall`)
- [ ] Velopack OnBeforeUninstallFastCallback fires: running PrintAgent process killed
- [ ] `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\PrintAgent.lnk` removed
- [ ] Trusted root cert for CN=localhost removed from CurrentUser store
- [ ] `%LOCALAPPDATA%\PrintAgent\` removed by Velopack
- [ ] `%APPDATA%\PrintAgent\` removed (user data -- may be left intentionally by Velopack; verify behavior)

## Auto-update (V1 note)

Auto-update is not yet active in V1 (no release feed URL configured). The RELEASES file and .nupkg are produced by build.ps1 for future use. Manual update: run the new `PrintAgentSetup.exe` over the existing installation.
