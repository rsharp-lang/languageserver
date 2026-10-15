Imports System
Imports languageserver

Module Program
    ''' <summary>
    ''' entry point of the R# language server. talks the Language Server Protocol
    ''' over stdin/stdout (JSON-RPC, Content-Length framed). all log output is sent
    ''' to stderr so the stdout stream stays reserved for protocol frames.
    ''' </summary>
    Sub Main(args As String())
        Try
            Dim server As New RsharpLspServer(Console.Error)
            ' keep Main synchronous: this VB compiler does not recognise an
            ' Async Main entry point, so we block on the async loop instead.
            server.RunAsync().GetAwaiter().GetResult()
        Catch ex As Exception
            Console.Error.WriteLine("[rsharp-lsp] fatal: " & ex.Message)
            Environment.Exit(1)
        End Try
    End Sub
End Module
