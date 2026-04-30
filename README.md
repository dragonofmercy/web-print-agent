# Web Print Agent

[![](https://img.shields.io/github/license/dragonofmercy/web-print-agent)](https://github.com/dragonofmercy/web-print-agent/blob/main/LICENSE)

Windows tray agent (.NET 8) that exposes a `wss://` JSON-RPC API on `localhost` so paired HTTPS web pages can list local printers and silent-print PDFs without going through the browser dialog.

Inspired by tools such as QZ Tray and Dymo Web Service, but kept intentionally minimal and focused on PDF printing.

## How it works

```
+------------------------------------+         +-----------------------------------+
|  Web page (https://app.example.com)|         |  Windows desktop                  |
|                                    |         |                                   |
|  await pa.getLocalPrinters()       | <-----> |  PrintAgent.exe (system tray)     |
|  await pa.print({ pdfBase64, ... })|  wss:// |    Kestrel + self-signed cert     |
|                                    |  127.   |    Origin pairing prompt          |
|  printagent-client.js              |  0.0.1: |    SumatraPDF (silent printing)   |
+------------------------------------+  8443+  +-----------------------------------+
```

1. The user installs the agent once. A self-signed certificate is added to the user's *Trusted Root* store so browsers accept `wss://localhost`.
2. A web page imports the bundled TypeScript client and calls `connect()`. The first time a given origin connects, the agent shows a Windows prompt asking the user to authorize that origin.
3. Once paired, the page can list installed printers, send PDF jobs, and receive asynchronous events (`job.statusChanged`, `printers.changed`).

## Repository layout

| Folder        | Purpose                                                    |
|---------------|------------------------------------------------------------|
| `agent/`      | .NET 8 solution: the tray agent + xUnit test project.      |
| `client/`     | Standalone TypeScript client (no npm publish, copy-paste). |
| `installer/`  | Velopack build script producing the Windows installer.     |

## Build

### Prerequisites

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org) (for building the TypeScript client)
- `.NET tool: vpk` — auto-installed globally by `build.ps1` if not present (`dotnet tool install -g vpk`)
- [SumatraPDF](https://www.sumatrapdfreader.org/download-free-pdf-viewer) — **already vendored** at `agent/PrintAgent/Resources/SumatraPDF.exe` (3.6.1, ~19 MB, portable 64-bit). It is embedded at compile time as a resource and extracted to `%APPDATA%\PrintAgent\bin\` on first run. To bump the version, replace the file and update `SUMATRAPDF-NOTICE.txt`.

### Build the agent

```sh
cd agent
dotnet publish PrintAgent/PrintAgent.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

Output: `agent/PrintAgent/bin/Release/net8.0-windows/win-x64/publish/PrintAgent.exe` (~90 MB self-contained, including the embedded SumatraPDF binary).

### Run the test suite

```sh
cd agent
dotnet test
```

### Build the TypeScript client

```sh
cd client
npm install
npm run build
```

Output: `client/dist/printagent-client.js` and `printagent-client.d.ts`. These two files are also committed to the repo so consumers can grab them directly without running the build.

### Build the installer

```sh
cd installer
./build.ps1 -Version 0.1.0
```

Output in `installer/Output/`:
- `PrintAgentSetup.exe` — run on the target machine to install (per-user, no elevation required)
- `RELEASES` — Velopack release feed for auto-update
- `PrintAgent-0.1.0-full.nupkg` — delta update package

The installer is built with [Velopack](https://velopack.io/). The `vpk` dotnet tool is installed automatically by `build.ps1` if not already present.

## Test page

A standalone HTML demo is shipped alongside the client at `client/dist/index.html`. It connects to a running PrintAgent, lists printers, lets you upload a PDF and watch job events stream in.

```sh
cd client/dist
npx http-server -p 8080
# then open http://localhost:8080/
```

Note: the page must be served over HTTPS (or from a `https://*.dev.localhost` vhost trusted by your browser) so the `wss://127.0.0.1:8443` connection passes the same-origin and mixed-content checks. By default the agent rejects `http://` origins; flip `AllowInsecureOrigins` to `true` in `appsettings.json` only for local dev.

## Using the client in a web page

Copy `client/dist/printagent-client.js` and `printagent-client.d.ts` into your project (for example under `src/lib/`).

```ts
import { PrintAgent } from './lib/printagent-client'

const pa = new PrintAgent()
await pa.connect() // user sees the pairing prompt the first time

const printers = await pa.getLocalPrinters()
const { jobId } = await pa.print({
    printerName: 'HP LaserJet',
    pdfBase64,
    options: { copies: 1, paperSize: 'A4' }
})

pa.on('job.statusChanged', (event) => {
    if (event.jobId === jobId) console.log(event.status)
})
```

## Credits

- Maintained by [DragonOfMercy](https://github.com/dragonofmercy)

## License

This package is open-sourced software licensed under the [MIT](LICENSE) license.

### Third-party software

PrintAgent embeds a copy of [SumatraPDF](https://www.sumatrapdfreader.org/) (3.6.1) for silent PDF printing. SumatraPDF is licensed under the GNU GPL v3.0 or later. PrintAgent invokes it as a separate subprocess, so the two programs form an aggregate under section 5 of the GPL and PrintAgent's MIT license is unaffected. See [agent/PrintAgent/Resources/SUMATRAPDF-NOTICE.txt](agent/PrintAgent/Resources/SUMATRAPDF-NOTICE.txt) for the full attribution and links to the source code.

## Support

If this project helps to increase your productivity, you can give me a cup of coffee :)

<a href="https://ko-fi.com/dragonofmercy" target="_blank"><img src="https://cdn.ko-fi.com/cdn/kofi2.png?v=3" alt="Donate" width="160px" /></a>