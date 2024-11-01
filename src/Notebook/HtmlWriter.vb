#Region "Microsoft.VisualBasic::e90afbdb814b02098b94f28856f57d9c, Rnotebook\src\HtmlWriter.vb"

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

    ' Class HtmlWriter
    ' 
    '     Constructor: (+1 Overloads) Sub New
    '     Function: GetHtml
    ' 
    ' /********************************************************************************/

#End Region

Imports System.Text
Imports Microsoft.VisualBasic.MIME.text.markdown
Imports Microsoft.VisualBasic.Text.Xml
Imports SMRUCC.Rsharp.Runtime

Public Class HtmlWriter

    Friend ReadOnly markdownEngine As New MarkdownRender
    Friend ReadOnly strict As Boolean = False

    Sub New(strict As Boolean)
        Me.strict = strict
    End Sub

    Public Function GetHtml(nb As Notebook, env As GlobalEnvironment) As String
        Dim html As New StringBuilder
        Dim session As New Interpreter(env, strict)

        For Each block As NoteBlock In nb.blocks
            If TypeOf block Is RCodeBlock Then
                Call html.AppendLine("<hr />")
            End If

            Call html.AppendLine(block.ToHtml(Me))
            Call html.AppendLine()

            If TypeOf block Is RCodeBlock Then
                Call html.AppendLine(sprintf(
                    <div class="output">
                        <code>
                            <pre>%s</pre>
                        </code>
                    </div>, session.GetHtmlOutput(block)))
                Call html.AppendLine("<hr />")
            Else
                Call html.AppendLine("<br />")
            End If
        Next

        Return html.ToString
    End Function
End Class

