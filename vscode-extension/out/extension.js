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
const client_1 = require("./client");
let client;
async function activate(context) {
    console.log('DiffPilot extension is activating...');
    // Initialize MCP client
    client = new client_1.DiffPilotClient(context);
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
            }
            catch (error) {
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
function deactivate() {
    if (client) {
        client.dispose();
        client = undefined;
    }
}
//# sourceMappingURL=extension.js.map