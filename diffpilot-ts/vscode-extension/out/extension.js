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
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const path = __importStar(require("path"));
async function activate(context) {
    console.log('DiffPilot extension is activating...');
    // Register MCP server definition provider for @mcp discovery
    const didChangeEmitter = new vscode.EventEmitter();
    const mcpProvider = vscode.lm.registerMcpServerDefinitionProvider('diffpilot', {
        onDidChangeMcpServerDefinitions: didChangeEmitter.event,
        provideMcpServerDefinitions: async () => {
            const config = vscode.workspace.getConfiguration('diffpilot');
            const customServerPath = config.get('serverPath');
            const nodePath = config.get('nodePath') || 'node';
            // Use bundled server or custom path
            const serverPath = customServerPath || path.join(context.extensionPath, 'server', 'index.js');
            // Get workspace folder for the server to use as working directory
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '';
            // Create MCP server definition with workspace folder as environment variable
            const serverDef = new vscode.McpStdioServerDefinition('DiffPilot', nodePath, [serverPath], { DIFFPILOT_WORKSPACE: workspaceFolder });
            return [serverDef];
        }
    });
    context.subscriptions.push(mcpProvider);
    context.subscriptions.push(didChangeEmitter);
    // Register commands (these are for manual invocation - MCP tools work automatically via Copilot)
    const commands = [
        { id: 'diffpilot.getPrDiff', tool: 'get_pr_diff' },
        { id: 'diffpilot.reviewPrChanges', tool: 'review_pr_changes' },
        { id: 'diffpilot.generatePrTitle', tool: 'generate_pr_title' },
        { id: 'diffpilot.generatePrDescription', tool: 'generate_pr_description' },
        { id: 'diffpilot.generateCommitMessage', tool: 'generate_commit_message' },
        { id: 'diffpilot.scanSecrets', tool: 'scan_secrets' },
        { id: 'diffpilot.getDiffStats', tool: 'diff_stats' },
        { id: 'diffpilot.suggestTests', tool: 'suggest_tests' },
        { id: 'diffpilot.generateChangelog', tool: 'generate_changelog' },
    ];
    for (const cmd of commands) {
        const disposable = vscode.commands.registerCommand(cmd.id, async () => {
            // Show message that tool should be used via Copilot
            const action = await vscode.window.showInformationMessage(`Use this tool via GitHub Copilot: @workspace #${cmd.tool}`, 'Copy Prompt');
            if (action === 'Copy Prompt') {
                await vscode.env.clipboard.writeText(`@workspace #${cmd.tool}`);
                vscode.window.showInformationMessage('Prompt copied to clipboard!');
            }
        });
        context.subscriptions.push(disposable);
    }
    // Status bar item
    const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.text = '$(rocket) DiffPilot';
    statusBarItem.tooltip = 'DiffPilot - AI PR Code Review (TypeScript)';
    statusBarItem.command = 'workbench.action.quickOpen';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);
    console.log('DiffPilot extension activated!');
}
function deactivate() {
    // Cleanup if needed
}
//# sourceMappingURL=extension.js.map