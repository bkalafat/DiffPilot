import * as vscode from 'vscode';
import * as path from 'path';

export async function activate(context: vscode.ExtensionContext) {
    console.log('DiffPilot extension is activating...');
    
    // Register MCP server definition provider for @mcp discovery
    const didChangeEmitter = new vscode.EventEmitter<void>();
    const mcpProvider = vscode.lm.registerMcpServerDefinitionProvider('diffpilot', {
        onDidChangeMcpServerDefinitions: didChangeEmitter.event,
        provideMcpServerDefinitions: async () => {
            const config = vscode.workspace.getConfiguration('diffpilot');
            const customServerPath = config.get<string>('serverPath');
            const nodePath = config.get<string>('nodePath') || 'node';
            
            // Use bundled server or custom path
            const serverPath = customServerPath || path.join(context.extensionPath, 'server', 'index.js');
            
            // Get workspace folder for the server to use as working directory
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '';
            
            // Create MCP server definition with workspace folder as environment variable
            const serverDef = new vscode.McpStdioServerDefinition(
                'DiffPilot',
                nodePath,
                [serverPath],
                { DIFFPILOT_WORKSPACE: workspaceFolder }
            );
            
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
            const action = await vscode.window.showInformationMessage(
                `Use this tool via GitHub Copilot: @workspace #${cmd.tool}`,
                'Copy Prompt'
            );
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

export function deactivate() {
    // Cleanup if needed
}
