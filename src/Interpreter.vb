#Region "Microsoft.VisualBasic::a189d44d6064cc7fc055bd248b2ede6b, Rnotebook\src\Interpreter.vb"

    ' Author:
    ' 
    '       asuka (amethyst.asuka@gcmodeller.org)
    '       xie (genetics@smrucc.org)
    '       xieguigang (xie.guigang@live.com)
    ' 
    ' Copyright (c) 2018 GPL3 Licensed
    ' 
    ' 
    ' GNU GENERAL PUBLIC LICENSE (GPL3)
    ' 
    ' 
    ' This program is free software: you can redistribute it and/or modify
    ' it under the terms of the GNU General Public License as published by
    ' the Free Software Foundation, either version 3 of the License, or
    ' (at your option) any later version.
    ' 
    ' This program is distributed in the hope that it will be useful,
    ' but WITHOUT ANY WARRANTY; without even the implied warranty of
    ' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    ' GNU General Public License for more details.
    ' 
    ' You should have received a copy of the GNU General Public License
    ' along with this program. If not, see <http://www.gnu.org/licenses/>.



    ' /********************************************************************************/

    ' Summaries:

    ' Class Interpreter
    ' 
    '     Constructor: (+1 Overloads) Sub New
    '     Function: GetHtmlOutput
    ' 
    ' /********************************************************************************/

#End Region

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

    Sub New(env As GlobalEnvironment, strict As Boolean)
        REngine = New RInterpreter(New GlobalEnvironment(env))
        REngine.options(strict:=strict)
        REngine.redirectError2stdout = True
        REngine.globalEnvir.Rscript = REngine
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

