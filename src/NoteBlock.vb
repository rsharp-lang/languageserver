Imports System.Text
Imports Microsoft.VisualBasic.ComponentModel.Ranges.Model
Imports Microsoft.VisualBasic.Text
Imports Microsoft.VisualBasic.Text.Xml
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine

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
        Return sprintf(
            <code>
                <pre class="code-input">%s</pre>
            </code>, text)
    End Function
End Class