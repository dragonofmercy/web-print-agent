# PrintAgent client

Standalone TypeScript client for the [PrintAgent](https://github.com/dragonofmercy/web-print-agent) Windows tray agent.

## Build

```sh
npm install
npm run build
```

This produces `dist/printagent-client.js` (ESM bundle) and `dist/printagent-client.d.ts`. Both are committed to the repo so consumers can copy them directly without running the build.

## Usage (Vue 3 example)

```ts
import { PrintAgent } from '@/lib/printagent-client'

const pa = new PrintAgent()
await pa.connect() // user sees the pairing prompt the first time

const printers = await pa.getLocalPrinters()
const { jobId } = await pa.print({
    printerName: 'HP LaserJet',
    pdfBase64: base64String,
    options: { copies: 1, paperSize: 'A4' }
})

pa.on('job.statusChanged', (e) => {
    if (e.jobId === jobId) console.log(e.status)
})
```

## Behavior

- The client tries `wss://127.0.0.1:8443..8447` until one accepts.
- It calls `agent.hello` first; the agent may show a pairing prompt to the user. If approved, subsequent calls work; if refused, `connect()` rejects with `OriginNotAuthorized`.
- Auto-reconnects with exponential backoff on disconnect.
