# PrintAgent -- Manual Smoke Test Checklist

Run after each release build before tagging.

## Prerequisites

- [ ] `SumatraPDF.exe` placed at `build/agent/PrintAgent/Resources/SumatraPDF.exe`
- [ ] `dotnet publish` produced a self-contained single-file exe
- [ ] `iscc` produced the installer

## Fresh install

- [ ] Run `PrintAgentSetup-X.Y.Z.exe` on a clean Windows machine
- [ ] First launch: UAC prompt for trusted root cert appears -> Accept
- [ ] PrintAgent icon appears in the tray
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

- [ ] Reboot Windows -> PrintAgent starts automatically (Startup shortcut)
- [ ] Bound port preserved (`config.json.lastBoundPort`)

## Uninstall

- [ ] Apps & features -> Uninstall PrintAgent
- [ ] `%LOCALAPPDATA%\PrintAgent\` removed
- [ ] `%APPDATA%\PrintAgent\` removed
- [ ] Startup shortcut removed
- [ ] Trusted root cert for CN=localhost removed
