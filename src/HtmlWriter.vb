Imports System.Text
Imports Microsoft.VisualBasic.MIME.text.markdown
Imports Microsoft.VisualBasic.Text.Xml

Public Class HtmlWriter

    Friend ReadOnly markdownEngine As New MarkdownHTML

    Public Function GetHtml(nb As Notebook) As String
        Dim html As New StringBuilder
        Dim session As New Interpreter

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
