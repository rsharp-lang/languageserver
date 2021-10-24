Imports System.IO
Imports System.Text
Imports Microsoft.VisualBasic.Imaging.Driver
Imports Microsoft.VisualBasic.Net.Http
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

        If TypeOf result Is ImageData Then
            Using imgBuffer As New MemoryStream
                Call DirectCast(result, ImageData).Save(imgBuffer)
                Call imgBuffer.Flush()
                Call imgBuffer.Seek(0, SeekOrigin.Begin)
                Call text.WriteLine(
                    <div>
                        <img class="output-image" src=<%= $"data:image/png;charset=ascii;base64,{imgBuffer.ToBase64String}" %>/>
                    </div>)
            End Using

            Call text.Flush()
            Call text.Dispose()

            Return Encoding.UTF8.GetString(buffer.ToArray)
        Else
            Call Rscript.handleResult(result, REngine.globalEnvir, program)
            Call text.Flush()
            Call text.Dispose()

            Return Encoding.UTF8 _
                .GetString(buffer.ToArray) _
                .Replace("<", "&lt;")
        End If
    End Function

End Class
