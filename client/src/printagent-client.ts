export interface PrinterInfo {
  name: string;
  isDefault: boolean;
  status: string;
  paperSizes: string[];
}

export interface PrintOptions {
  copies?: number;
  paperSize?: string;
  color?: boolean;
  orientation?: 'portrait' | 'landscape';
}

export interface PrintRequest {
  printerName: string;
  pdfBase64: string;
  options?: PrintOptions;
}

export type JobStatus = 'Submitted' | 'Printing' | 'Completed' | 'Failed';

export interface JobEvent {
  jobId: string;
  status: JobStatus;
  error?: string;
}

export interface AgentHelloResult {
  agentVersion: string;
  capabilities: string[];
  jobEventsSupported: boolean;
}

export interface PrintAgentOptions {
  portRange?: number[];
  handshakeTimeoutMs?: number;
  reconnectInitialDelayMs?: number;
  reconnectMaxDelayMs?: number;
  clientVersion?: string;
}

type EventName = 'job.statusChanged' | 'printers.changed' | 'open' | 'close';
type EventListener = (data: any) => void;

export class PrintAgent {
  private opts: Required<PrintAgentOptions>;
  private ws: WebSocket | null = null;
  private nextId = 1;
  private pending = new Map<number | string, { resolve: (v: any) => void; reject: (e: any) => void }>();
  private listeners = new Map<EventName, EventListener[]>();
  private reconnectDelay: number;

  constructor(options: PrintAgentOptions = {}) {
    this.opts = {
      portRange: options.portRange ?? [8443, 8444, 8445, 8446, 8447],
      handshakeTimeoutMs: options.handshakeTimeoutMs ?? 30_000,
      reconnectInitialDelayMs: options.reconnectInitialDelayMs ?? 1_000,
      reconnectMaxDelayMs: options.reconnectMaxDelayMs ?? 30_000,
      clientVersion: options.clientVersion ?? '0.1.4',
    };
    this.reconnectDelay = this.opts.reconnectInitialDelayMs;
  }

  async connect(): Promise<AgentHelloResult> {
    const ws = await this.openSocket();
    this.ws = ws;
    ws.onmessage = (ev) => this.handleMessage(ev.data);
    ws.onclose = () => { this.ws = null; this.emit('close', null); this.scheduleReconnect(); };
    ws.onerror = () => { /* close handler will follow */ };
    const result = await this.request<AgentHelloResult>('agent.hello', { clientVersion: this.opts.clientVersion });
    this.emit('open', result);
    this.reconnectDelay = this.opts.reconnectInitialDelayMs;
    return result;
  }

  async getLocalPrinters(): Promise<PrinterInfo[]> {
    return this.request<PrinterInfo[]>('getLocalPrinters', {});
  }

  async print(req: PrintRequest): Promise<{ jobId: string }> {
    return this.request<{ jobId: string }>('print', req);
  }

  async getJobStatus(jobId: string): Promise<{ status: JobStatus; error?: string }> {
    return this.request<{ status: JobStatus; error?: string }>('getJobStatus', { jobId });
  }

  on(event: EventName, listener: EventListener): void {
    if (!this.listeners.has(event)) this.listeners.set(event, []);
    this.listeners.get(event)!.push(listener);
  }

  private emit(event: EventName, data: any): void {
    for (const l of this.listeners.get(event) ?? []) l(data);
  }

  private async openSocket(): Promise<WebSocket> {
    let lastError: unknown = null;
    for (const port of this.opts.portRange) {
      try {
        return await this.tryOpen(`wss://127.0.0.1:${port}/ws`);
      } catch (err) { lastError = err; }
    }
    throw new Error(`Unable to reach PrintAgent on any port: ${String(lastError)}`);
  }

  private tryOpen(url: string): Promise<WebSocket> {
    return new Promise((resolve, reject) => {
      const ws = new WebSocket(url);
      const timer = setTimeout(() => { ws.close(); reject(new Error('timeout')); }, 3_000);
      ws.onopen = () => { clearTimeout(timer); resolve(ws); };
      ws.onerror = (e) => { clearTimeout(timer); reject(e); };
    });
  }

  private request<T>(method: string, params: object): Promise<T> {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      return Promise.reject(new Error('Not connected'));
    }
    const id = this.nextId++;
    const message = JSON.stringify({ jsonrpc: '2.0', id, method, params });
    return new Promise<T>((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.pending.delete(id)) reject(new Error(`Request '${method}' timed out`));
      }, this.opts.handshakeTimeoutMs);
      this.pending.set(id, {
        resolve: (v) => { clearTimeout(timer); resolve(v); },
        reject: (e) => { clearTimeout(timer); reject(e); },
      });
      this.ws!.send(message);
    });
  }

  private handleMessage(raw: any): void {
    let msg: any;
    try { msg = JSON.parse(raw); } catch { return; }

    if (msg.id !== undefined && (msg.result !== undefined || msg.error !== undefined)) {
      const handler = this.pending.get(msg.id);
      if (!handler) return;
      this.pending.delete(msg.id);
      if (msg.error) handler.reject(new RpcError(msg.error.code, msg.error.message));
      else handler.resolve(msg.result);
      return;
    }

    if (typeof msg.method === 'string') {
      if (msg.method === 'job.statusChanged' || msg.method === 'printers.changed') {
        this.emit(msg.method as EventName, msg.params ?? null);
      }
    }
  }

  private scheduleReconnect(): void {
    const delay = this.reconnectDelay;
    this.reconnectDelay = Math.min(this.reconnectDelay * 2, this.opts.reconnectMaxDelayMs);
    setTimeout(() => { this.connect().catch(() => { /* retried again on close */ }); }, delay);
  }
}

export class RpcError extends Error {
  constructor(public code: number, message: string) { super(message); this.name = 'RpcError'; }
}
