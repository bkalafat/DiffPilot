import * as vscode from 'vscode';
import * as path from 'path';
import { DiffPilotClient } from './client';

let client: DiffPilotClient | undefined;

export async function activate(context: vscode.ExtensionContext) {
    console.log('DiffPilot extension is activating...');
    
    // Initialize MCP client
    client = new DiffPilotClient(context);
    
    // Register MCP server definition provider for @mcp discovery
    const didChangeEmitter = new vscode.EventEmitter<void>();
    const mcpProvider = vscode.lm.registerMcpServerDefinitionProvider('diffpilot', {
        onDidChangeMcpServerDefinitions: didChangeEmitter.event,
        provideMcpServerDefinitions: async () => {
            const config = vscode.workspace.getConfiguration('diffpilot');
            const customServerPath = config.get<string>('serverPath');
            const dotnetPath = config.get<string>('dotnetPath') || 'dotnet';
            
            // Use bundled server or custom path
            const serverPath = customServerPath || path.join(context.extensionPath, 'server');
            
            // Create MCP server definition using constructor with all required parameters
            const serverDef = new vscode.McpStdioServerDefinition(
                'DiffPilot',
                dotnetPath,
                ['run', '--project', serverPath],
                {} // environment variables
            );
            
            return [serverDef];
        }
    });
    context.subscriptions.push(mcpProvider);
    context.subscriptions.push(didChangeEmitter);
    
    // Register commands
    const commands = [
        { id: 'diffpilot.getPrDiff', handler: () => client?.callTool('get_pr_diff') },
        { id: 'diffpilot.reviewPrChanges', handler: () => client?.callTool('review_pr_changes') },
        { id: 'diffpilot.generatePrTitle', handler: () => client?.callTool('generate_pr_title') },
        { id: 'diffpilot.generatePrDescription', handler: () => client?.callTool('generate_pr_description') },
        { id: 'diffpilot.generateCommitMessage', handler: () => client?.callTool('generate_commit_message') },
        { id: 'diffpilot.scanSecrets', handler: () => client?.callTool('scan_secrets') },
        { id: 'diffpilot.getDiffStats', handler: () => client?.callTool('diff_stats') },
        { id: 'diffpilot.suggestTests', handler: () => client?.callTool('suggest_tests') },
        { id: 'diffpilot.generateChangelog', handler: () => client?.callTool('generate_changelog') },
    ];

    for (const cmd of commands) {
        const disposable = vscode.commands.registerCommand(cmd.id, async () => {
            try {
                await cmd.handler();
            } catch (error) {
                vscode.window.showErrorMessage(`DiffPilot error: ${error}`);
            }
        });
        context.subscriptions.push(disposable);
    }

    // Status bar item
    const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.text = '$(rocket) DiffPilot';
    statusBarItem.tooltip = 'DiffPilot - AI PR Code Review';
    statusBarItem.command = 'workbench.action.quickOpen';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    console.log('DiffPilot extension activated!');
}

export function deactivate() {
    if (client) {
        client.dispose();
        client = undefined;
    }
}
