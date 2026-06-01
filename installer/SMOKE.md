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
- [ ] `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\PrintAgent.lnk` does NOT exist (auto-start is disabled)
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

- [ ] Reboot Windows -> PrintAgent does NOT start automatically (must be launched manually)
- [ ] After manual launch, bound port preserved (`config.json.lastBoundPort`)

## Uninstall

- [ ] Apps & features -> Uninstall PrintAgent (or run `PrintAgentSetup.exe --uninstall`)
- [ ] Velopack OnBeforeUninstallFastCallback fires: running PrintAgent process killed
- [ ] `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\PrintAgent.lnk` removed (legacy, in case an older version had created it)
- [ ] Trusted root cert for CN=localhost removed from CurrentUser store
- [ ] `%LOCALAPPDATA%\PrintAgent\` removed by Velopack
- [ ] `%APPDATA%\PrintAgent\` removed (user data -- may be left intentionally by Velopack; verify behavior)

## Auto-update (manual)

Prereq: build two installers with different versions, e.g. `./build.ps1 -Version 0.1.2` then `./build.ps1 -Version 0.1.3`, and a GitHub release feed reachable at the configured `UpdateRepoUrl`.

1. Install the OLDER version (0.1.2) via its `PrintAgentSetup.exe`. Confirm the tray icon appears.
2. Publish the NEWER version (0.1.3) `RELEASES` + `.nupkg` to the GitHub release.
3. Tray menu -> "Check for updates...". Within a few seconds a Windows toast appears: "Update 0.1.3 is ready. Click to restart now."
4. With NO print job running: click the toast. The agent restarts. Re-open About -> version reads 0.1.3.
5. Busy path: install 0.1.2 again, start a long print job, publish 0.1.3, "Check for updates...", click the toast WHILE printing -> toast "A print job is running. The update will be applied later." The job finishes uninterrupted; the update applies on the next restart.
6. Silent path: install 0.1.2, "Check for updates..." to stage 0.1.3, do NOT click, quit and relaunch the agent -> it starts on 0.1.3 (applied silently at boot).
7. Kill switch: set `"AutoUpdate": false` in `%APPDATA%\PrintAgent\config.json`, relaunch, "Check for updates..." -> no check occurs (verify via logs: "Auto-update disabled by configuration.").
