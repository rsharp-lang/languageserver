Imports System.IO
Imports System.Text
Imports SMRUCC.Rsharp.Interpreter
Imports SMRUCC.Rsharp.Runtime

''' <summary>
''' the R# code block interpreter
''' </summary>
Public Class Interpreter

    ReadOnly REngine As RInterpreter

    Sub New()
        REngine = New RInterpreter
    End Sub

    Public Function GetHtmlOutput(code As RCodeBlock) As String
        Dim buffer As New MemoryStream
        Dim text As New StreamWriter(buffer, Encoding.UTF8)
        Dim program As New Program(code.block)
        Dim result As Object = REngine _
            .RedirectOutput(text, OutputEnvironments.Html) _
            .Run(program)

        Call Rscript.handleResult(result, REngine.globalEnvir, program)

        Call text.Flush()
        Call text.Dispose()

        Return Encoding.UTF8.GetString(buffer.ToArray)
    End Function

End Class
