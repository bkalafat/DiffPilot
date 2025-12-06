"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.DiffPilotClient = void 0;
const vscode = __importStar(require("vscode"));
const cp = __importStar(require("child_process"));
const path = __importStar(require("path"));
class DiffPilotClient {
    context;
    process;
    requestId = 0;
    pendingRequests = new Map();
    buffer = '';
    outputChannel;
    constructor(context) {
        this.context = context;
        this.outputChannel = vscode.window.createOutputChannel('DiffPilot');
    }
    async ensureStarted() {
        if (this.process && !this.process.killed) {
            return;
        }
        const config = vscode.workspace.getConfiguration('diffpilot');
        const dotnetPath = config.get('dotnetPath') || 'dotnet';
        let serverPath = config.get('serverPath') || '';
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
        this.process.stdout?.on('data', (data) => {
            this.handleData(data.toString());
        });
        this.process.stderr?.on('data', (data) => {
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
    handleData(data) {
        this.buffer += data;
        const lines = this.buffer.split('\n');
        this.buffer = lines.pop() || '';
        for (const line of lines) {
            if (line.trim()) {
                try {
                    const response = JSON.parse(line);
                    const pending = this.pendingRequests.get(response.id);
                    if (pending) {
                        this.pendingRequests.delete(response.id);
                        if (response.error) {
                            pending.reject(new Error(response.error.message));
                        }
                        else {
                            pending.resolve(response.result);
                        }
                    }
                }
                catch (e) {
                    this.outputChannel.appendLine(`Parse error: ${e}`);
                }
            }
        }
    }
    sendRequest(method, params) {
        return new Promise((resolve, reject) => {
            if (!this.process || this.process.killed) {
                reject(new Error('Server not running'));
                return;
            }
            const id = ++this.requestId;
            const request = {
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
    async initialize() {
        const result = await this.sendRequest('initialize', {
            protocolVersion: '2024-11-05',
            capabilities: {},
            clientInfo: { name: 'vscode-diffpilot', version: '1.0.0' }
        });
        this.outputChannel.appendLine(`Initialized: ${JSON.stringify(result)}`);
    }
    async callTool(toolName, args) {
        try {
            await this.ensureStarted();
            const config = vscode.workspace.getConfiguration('diffpilot');
            // Add default arguments based on settings
            const toolArgs = { ...args };
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
            });
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
                const action = await vscode.window.showInformationMessage('Commit message generated!', 'Copy to Clipboard', 'Use in SCM');
                if (action === 'Copy to Clipboard') {
                    await vscode.env.clipboard.writeText(text);
                    vscode.window.showInformationMessage('Copied to clipboard!');
                }
                else if (action === 'Use in SCM') {
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
                const action = await vscode.window.showInformationMessage('PR title generated!', 'Copy to Clipboard');
                if (action === 'Copy to Clipboard') {
                    await vscode.env.clipboard.writeText(text);
                    vscode.window.showInformationMessage('Copied to clipboard!');
                }
            }
            // For secrets scan, show warning if secrets found
            if (toolName === 'scan_secrets' && text.includes('potential secret')) {
                vscode.window.showWarningMessage('⚠️ Potential secrets detected! Review the output before committing.', 'Show Output').then(action => {
                    if (action === 'Show Output') {
                        this.outputChannel.show();
                    }
                });
            }
        }
        catch (error) {
            this.outputChannel.appendLine(`Error: ${error}`);
            vscode.window.showErrorMessage(`DiffPilot error: ${error}`);
        }
    }
    getLanguageForTool(toolName) {
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
    dispose() {
        if (this.process && !this.process.killed) {
            this.process.kill();
        }
        this.outputChannel.dispose();
    }
}
exports.DiffPilotClient = DiffPilotClient;
//# sourceMappingURL=client.js.map