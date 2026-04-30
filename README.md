# Web Print Agent

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
| `installer/`  | Inno Setup script producing the Windows installer.         |

## Build

### Prerequisites

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org) (for building the TypeScript client)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (for the installer)
- [SumatraPDF](https://www.sumatrapdfreader.org/download-free-pdf-viewer) — download the **portable 64-bit** build and copy `SumatraPDF.exe` to `agent/PrintAgent/Resources/`. This file is gitignored.

### Build the agent

```sh
cd agent
dotnet publish PrintAgent/PrintAgent.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

Output: `agent/PrintAgent/bin/Release/net8.0-windows/win-x64/publish/PrintAgent.exe` (~70 MB, self-contained).

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
iscc PrintAgent.iss
```

Output: `installer/Output/PrintAgentSetup-X.Y.Z.exe`.

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

## License

[MIT](LICENSE) — Copyright (c) 2026 Dragon.
