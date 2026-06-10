// src/printagent-client.ts
var PrintAgent = class {
  constructor(options = {}) {
    this.ws = null;
    this.nextId = 1;
    this.pending = /* @__PURE__ */ new Map();
    this.listeners = /* @__PURE__ */ new Map();
    this.opts = {
      portRange: options.portRange ?? [8443, 8444, 8445, 8446, 8447],
      handshakeTimeoutMs: options.handshakeTimeoutMs ?? 3e4,
      reconnectInitialDelayMs: options.reconnectInitialDelayMs ?? 1e3,
      reconnectMaxDelayMs: options.reconnectMaxDelayMs ?? 3e4,
      clientVersion: options.clientVersion ?? "0.1.4"
    };
    this.reconnectDelay = this.opts.reconnectInitialDelayMs;
  }
  async connect() {
    const ws = await this.openSocket();
    this.ws = ws;
    ws.onmessage = (ev) => this.handleMessage(ev.data);
    ws.onclose = () => {
      this.ws = null;
      this.emit("close", null);
      this.scheduleReconnect();
    };
    ws.onerror = () => {
    };
    const result = await this.request("agent.hello", { clientVersion: this.opts.clientVersion });
    this.emit("open", result);
    this.reconnectDelay = this.opts.reconnectInitialDelayMs;
    return result;
  }
  async getLocalPrinters() {
    return this.request("getLocalPrinters", {});
  }
  async print(req) {
    return this.request("print", req);
  }
  async getJobStatus(jobId) {
    return this.request("getJobStatus", { jobId });
  }
  on(event, listener) {
    if (!this.listeners.has(event)) this.listeners.set(event, []);
    this.listeners.get(event).push(listener);
  }
  emit(event, data) {
    for (const l of this.listeners.get(event) ?? []) l(data);
  }
  async openSocket() {
    let lastError = null;
    for (const port of this.opts.portRange) {
      try {
        return await this.tryOpen(`wss://127.0.0.1:${port}/ws`);
      } catch (err) {
        lastError = err;
      }
    }
    throw new Error(`Unable to reach PrintAgent on any port: ${String(lastError)}`);
  }
  tryOpen(url) {
    return new Promise((resolve, reject) => {
      const ws = new WebSocket(url);
      const timer = setTimeout(() => {
        ws.close();
        reject(new Error("timeout"));
      }, 3e3);
      ws.onopen = () => {
        clearTimeout(timer);
        resolve(ws);
      };
      ws.onerror = (e) => {
        clearTimeout(timer);
        reject(e);
      };
    });
  }
  request(method, params) {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) {
      return Promise.reject(new Error("Not connected"));
    }
    const id = this.nextId++;
    const message = JSON.stringify({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        if (this.pending.delete(id)) reject(new Error(`Request '${method}' timed out`));
      }, this.opts.handshakeTimeoutMs);
      this.pending.set(id, {
        resolve: (v) => {
          clearTimeout(timer);
          resolve(v);
        },
        reject: (e) => {
          clearTimeout(timer);
          reject(e);
        }
      });
      this.ws.send(message);
    });
  }
  handleMessage(raw) {
    let msg;
    try {
      msg = JSON.parse(raw);
    } catch {
      return;
    }
    if (msg.id !== void 0 && (msg.result !== void 0 || msg.error !== void 0)) {
      const handler = this.pending.get(msg.id);
      if (!handler) return;
      this.pending.delete(msg.id);
      if (msg.error) handler.reject(new RpcError(msg.error.code, msg.error.message));
      else handler.resolve(msg.result);
      return;
    }
    if (typeof msg.method === "string") {
      if (msg.method === "job.statusChanged" || msg.method === "printers.changed") {
        this.emit(msg.method, msg.params ?? null);
      }
    }
  }
  scheduleReconnect() {
    const delay = this.reconnectDelay;
    this.reconnectDelay = Math.min(this.reconnectDelay * 2, this.opts.reconnectMaxDelayMs);
    setTimeout(() => {
      this.connect().catch(() => {
      });
    }, delay);
  }
};
var RpcError = class extends Error {
  constructor(code, message) {
    super(message);
    this.code = code;
    this.name = "RpcError";
  }
};
export {
  PrintAgent,
  RpcError
};
