#Region "Microsoft.VisualBasic::49057a693ea71c8010345203833d3a2a, Rnotebook\src\NoteBlock.vb"

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

    ' Class NoteBlock
    ' 
    ' 
    ' 
    ' Class MarkdownBlock
    ' 
    '     Properties: markdown
    ' 
    '     Function: GetScript, ToHtml, ToString
    ' 
    ' Class RCodeBlock
    ' 
    '     Properties: block, region, regionName, text
    ' 
    '     Function: GetScript, ToHtml, ToString, Trim
    ' 
    ' /********************************************************************************/

#End Region

Imports System.Text
Imports Microsoft.VisualBasic.ComponentModel.Ranges.Model
Imports Microsoft.VisualBasic.Text.Xml
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports ASCII = Microsoft.VisualBasic.Text.ASCII

Public MustInherit Class NoteBlock

    Public MustOverride Function GetScript() As String
    Public MustOverride Function ToHtml(render As HtmlWriter) As String

End Class

Public Class MarkdownBlock : Inherits NoteBlock

    Public Property markdown As String

    Public Overrides Function ToString() As String
        Return markdown
    End Function

    Public Overrides Function GetScript() As String
        Return markdown.LineTokens.Select(Function(line) $"# {line}").JoinBy(ASCII.LF) & vbCrLf & ";"
    End Function

    Public Overrides Function ToHtml(render As HtmlWriter) As String
        Return render.markdownEngine.Transform(markdown)
    End Function
End Class

Public Class RCodeBlock : Inherits NoteBlock

    Public Property block As Expression()
    ''' <summary>
    ''' the line range
    ''' </summary>
    ''' <returns></returns>
    Public Property region As IntRange
    Public Property text As String
    Public Property regionName As String

    Public Overrides Function ToString() As String
        Return text
    End Function

    Friend Function Trim() As RCodeBlock
        Dim txtLines As String() = Strings.Trim(text).LineTokens

        Do While txtLines.Length > 0 AndAlso (txtLines(Scan0).StartsWith("#region") OrElse Strings.Trim(txtLines(Scan0)).StringEmpty)
            txtLines = txtLines.Skip(1).ToArray
        Loop

        text = txtLines _
            .Where(Function(str)
                       Return Strings.Trim(str) <> "#end region"
                   End Function) _
            .JoinBy(vbCrLf)

        Return Me
    End Function

    Public Overrides Function GetScript() As String
        Dim sb As New StringBuilder

        sb.AppendLine($"#region ""{regionName}""")
        sb.AppendLine()
        sb.AppendLine(text)
        sb.AppendLine()
        sb.AppendLine("#end region")

        Return sb.ToString
    End Function

    Public Overrides Function ToHtml(render As HtmlWriter) As String
        Return sprintf(<pre><code class="code-input language-r hljs">%s</code></pre>, text)
    End Function
End Class
