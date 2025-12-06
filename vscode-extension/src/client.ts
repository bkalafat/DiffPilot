import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';

interface JsonRpcRequest {
    jsonrpc: '2.0';
    id: number;
    method: string;
    params?: unknown;
}

interface JsonRpcResponse {
    jsonrpc: '2.0';
    id: number;
    result?: {
        content: Array<{ type: string; text: string }>;
        isError: boolean;
    };
    error?: {
        code: number;
        message: string;
    };
}

export class DiffPilotClient {
    private process: cp.ChildProcess | undefined;
    private requestId = 0;
    private pendingRequests = new Map<number, { resolve: (value: unknown) => void; reject: (reason: unknown) => void }>();
    private buffer = '';
    private outputChannel: vscode.OutputChannel;

    constructor(private context: vscode.ExtensionContext) {
        this.outputChannel = vscode.window.createOutputChannel('DiffPilot');
    }

    private async ensureStarted(): Promise<void> {
        if (this.process && !this.process.killed) {
            return;
        }

        const config = vscode.workspace.getConfiguration('diffpilot');
        const dotnetPath = config.get<string>('dotnetPath') || 'dotnet';
        let serverPath = config.get<string>('serverPath') || '';

        // If no custom path, use bundled server
        if (!serverPath) {
            serverPath = path.join(this.context.extensionPath, 'server', 'DiffPilot.csproj');
        }

        const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!workspaceFolder) {
            throw new Error('No workspace folder open');
        }

        this.outputChannel.appendLine(`Starting DiffPilot server...`);
        this.outputChannel.appendLine(`Server path: ${serverPath}`);
        this.outputChannel.appendLine(`Working directory: ${workspaceFolder}`);

        this.process = cp.spawn(dotnetPath, ['run', '--project', serverPath], {
            cwd: workspaceFolder,
            stdio: ['pipe', 'pipe', 'pipe']
        });

        this.process.stdout?.on('data', (data: Buffer) => {
            this.handleData(data.toString());
        });

        this.process.stderr?.on('data', (data: Buffer) => {
            this.outputChannel.appendLine(`[stderr] ${data.toString()}`);
        });

        this.process.on('error', (error) => {
            this.outputChannel.appendLine(`Process error: ${error.message}`);
            vscode.window.showErrorMessage(`DiffPilot server error: ${error.message}`);
        });

        this.process.on('exit', (code) => {
            this.outputChannel.appendLine(`Server exited with code ${code}`);
            this.process = undefined;
        });

        // Wait for server to be ready
        await this.initialize();
    }

    private handleData(data: string): void {
        this.buffer += data;
        
        const lines = this.buffer.split('\n');
        this.buffer = lines.pop() || '';

        for (const line of lines) {
            if (line.trim()) {
                try {
                    const response = JSON.parse(line) as JsonRpcResponse;
                    const pending = this.pendingRequests.get(response.id);
                    if (pending) {
                        this.pendingRequests.delete(response.id);
                        if (response.error) {
                            pending.reject(new Error(response.error.message));
                        } else {
                            pending.resolve(response.result);
                        }
                    }
                } catch (e) {
                    this.outputChannel.appendLine(`Parse error: ${e}`);
                }
            }
        }
    }

    private sendRequest(method: string, params?: unknown): Promise<unknown> {
        return new Promise((resolve, reject) => {
            if (!this.process || this.process.killed) {
                reject(new Error('Server not running'));
                return;
            }

            const id = ++this.requestId;
            const request: JsonRpcRequest = {
                jsonrpc: '2.0',
                id,
                method,
                params
            };

            this.pendingRequests.set(id, { resolve, reject });
            this.process.stdin?.write(JSON.stringify(request) + '\n');

            // Timeout after 30 seconds
            setTimeout(() => {
                if (this.pendingRequests.has(id)) {
                    this.pendingRequests.delete(id);
                    reject(new Error('Request timeout'));
                }
            }, 30000);
        });
    }

    private async initialize(): Promise<void> {
        const result = await this.sendRequest('initialize', {
            protocolVersion: '2024-11-05',
            capabilities: {},
            clientInfo: { name: 'vscode-diffpilot', version: '1.0.0' }
        });
        this.outputChannel.appendLine(`Initialized: ${JSON.stringify(result)}`);
    }

    public async callTool(toolName: string, args?: Record<string, unknown>): Promise<void> {
        try {
            await this.ensureStarted();

            const config = vscode.workspace.getConfiguration('diffpilot');
            
            // Add default arguments based on settings
            const toolArgs: Record<string, unknown> = { ...args };
            
            if (toolName === 'generate_pr_title' && !toolArgs.style) {
                toolArgs.style = config.get('prTitleStyle');
            }
            if (toolName === 'generate_commit_message' && !toolArgs.style) {
                toolArgs.style = config.get('commitMessageStyle');
            }
            if (toolName === 'generate_pr_description' && toolArgs.includeChecklist === undefined) {
                toolArgs.includeChecklist = config.get('includeChecklist');
            }

            this.outputChannel.appendLine(`Calling tool: ${toolName}`);
            this.outputChannel.show();

            const result = await this.sendRequest('tools/call', {
                name: toolName,
                arguments: toolArgs
            }) as { content: Array<{ type: string; text: string }>; isError: boolean };

            if (result?.isError) {
                vscode.window.showErrorMessage(`DiffPilot: ${result.content[0]?.text || 'Unknown error'}`);
                return;
            }

            const text = result?.content?.map(c => c.text).join('\n') || '';
            
            // Show result in output channel
            this.outputChannel.appendLine('--- Result ---');
            this.outputChannel.appendLine(text);
            this.outputChannel.appendLine('--------------');

            // Also show in a new document for easier viewing
            const doc = await vscode.workspace.openTextDocument({
                content: text,
                language: this.getLanguageForTool(toolName)
            });
            await vscode.window.showTextDocument(doc, { preview: true });

            // For commit messages, offer to copy to clipboard
            if (toolName === 'generate_commit_message') {
                const action = await vscode.window.showInformationMessage(
                    'Commit message generated!',
                    'Copy to Clipboard',
                    'Use in SCM'
                );
                if (action === 'Copy to Clipboard') {
                    await vscode.env.clipboard.writeText(text);
                    vscode.window.showInformationMessage('Copied to clipboard!');
                } else if (action === 'Use in SCM') {
                    // Set SCM input box
                    const gitExtension = vscode.extensions.getExtension('vscode.git');
                    if (gitExtension?.isActive) {
                        const git = gitExtension.exports.getAPI(1);
                        const repo = git.repositories[0];
                        if (repo) {
                            repo.inputBox.value = text;
                        }
                    }
                }
            }

            // For PR title, offer to copy
            if (toolName === 'generate_pr_title') {
                const action = await vscode.window.showInformationMessage(
                    'PR title generated!',
                    'Copy to Clipboard'
                );
                if (action === 'Copy to Clipboard') {
                    await vscode.env.clipboard.writeText(text);
                    vscode.window.showInformationMessage('Copied to clipboard!');
                }
            }

            // For secrets scan, show warning if secrets found
            if (toolName === 'scan_secrets' && text.includes('potential secret')) {
                vscode.window.showWarningMessage(
                    '⚠️ Potential secrets detected! Review the output before committing.',
                    'Show Output'
                ).then(action => {
                    if (action === 'Show Output') {
                        this.outputChannel.show();
                    }
                });
            }

        } catch (error) {
            this.outputChannel.appendLine(`Error: ${error}`);
            vscode.window.showErrorMessage(`DiffPilot error: ${error}`);
        }
    }

    private getLanguageForTool(toolName: string): string {
        switch (toolName) {
            case 'get_pr_diff':
            case 'review_pr_changes':
                return 'diff';
            case 'generate_changelog':
            case 'generate_pr_description':
                return 'markdown';
            case 'diff_stats':
                return 'json';
            default:
                return 'plaintext';
        }
    }

    public dispose(): void {
        if (this.process && !this.process.killed) {
            this.process.kill();
        }
        this.outputChannel.dispose();
    }
}
