Imports System.Text
Imports Microsoft.VisualBasic.MIME.text.markdown

Public Class HtmlWriter

    Friend ReadOnly markdownEngine As New MarkdownHTML

    Public Function GetHtml(nb As Notebook) As String
        Dim html As New StringBuilder

        For Each block As NoteBlock In nb.blocks
            Call html.AppendLine(block.ToHtml(Me))
            Call html.AppendLine()
        Next

        Return html.ToString
    End Function
End Class
