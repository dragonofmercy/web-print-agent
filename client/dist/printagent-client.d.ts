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
export declare class PrintAgent {
    private opts;
    private ws;
    private nextId;
    private pending;
    private listeners;
    private reconnectDelay;
    constructor(options?: PrintAgentOptions);
    connect(): Promise<AgentHelloResult>;
    getLocalPrinters(): Promise<PrinterInfo[]>;
    print(req: PrintRequest): Promise<{
        jobId: string;
    }>;
    getJobStatus(jobId: string): Promise<{
        status: JobStatus;
        error?: string;
    }>;
    on(event: EventName, listener: EventListener): void;
    private emit;
    private openSocket;
    private tryOpen;
    private request;
    private handleMessage;
    private scheduleReconnect;
}
export declare class RpcError extends Error {
    code: number;
    constructor(code: number, message: string);
}
export {};
