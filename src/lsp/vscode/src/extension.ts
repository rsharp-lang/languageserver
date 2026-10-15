import * as path from 'path';
import { workspace, ExtensionContext } from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
    const config = workspace.getConfiguration('rsharp.lsp');
    const dotnet = (config.get<string>('dotnetPath') || 'dotnet').trim();

    let serverPath = (config.get<string>('serverPath') || '').trim();
    if (!serverPath) {
        // fall back to a server/lsp.dll shipped next to the extension
        serverPath = path.join(context.extensionPath, 'server', 'lsp.dll');
    }

    const serverOptions: ServerOptions = {
        command: dotnet,
        args: [serverPath],
        transport: TransportKind.stdio
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'rsharp' }],
        synchronize: {
            fileEvents: workspace.createFileSystemWatcher('**/*.rsharp')
        }
    };

    client = new LanguageClient(
        'rsharp-lsp',
        'R# Language Server',
        serverOptions,
        clientOptions
    );

    client.start();
}

export function deactivate(): Thenable<void> | undefined {
    if (!client) {
        return undefined;
    }
    return client.stop();
}
