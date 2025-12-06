import * as vscode from 'vscode';
export declare class DiffPilotClient {
    private context;
    private process;
    private requestId;
    private pendingRequests;
    private buffer;
    private outputChannel;
    constructor(context: vscode.ExtensionContext);
    private ensureStarted;
    private handleData;
    private sendRequest;
    private initialize;
    callTool(toolName: string, args?: Record<string, unknown>): Promise<void>;
    private getLanguageForTool;
    dispose(): void;
}
//# sourceMappingURL=client.d.ts.map