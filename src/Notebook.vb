Imports System.Text
Imports Microsoft.VisualBasic.ComponentModel.Ranges.Model
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Scripting.TokenIcer
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols
Imports R = SMRUCC.Rsharp.Runtime.Components.Rscript

''' <summary>
''' A notebook data object which is parsed from the R# script.
''' </summary>
Public Class Notebook

    Public ReadOnly Property blocks As NoteBlock()
        Get
            Return m_list.ToArray
        End Get
    End Property

    Dim m_list As New List(Of NoteBlock)

    Public Sub Add(block As NoteBlock)
        m_list.Add(block)
    End Sub

    Public Function CreateRscript() As String
        Dim sb As New StringBuilder

        For Each block As NoteBlock In blocks
            Call sb.AppendLine(block.GetScript)
            Call sb.AppendLine()
        Next

        Return sb.ToString
    End Function

    Public Shared Function fromRscript(rscript As R) As Notebook
        Dim lines As Expression() = Expression.ParseLines(rscript, keepsCommentLines:=True).ToArray
        Dim blockList As New List(Of NoteBlock)
        Dim regionMark As Boolean = False
        Dim codeBlock As New List(Of Expression)
        Dim start As CodeSpan = Nothing
        Dim regionName As String = Nothing

        For Each line As Expression In lines
            If TypeOf line Is CodeComment Then
                Select Case DirectCast(line, CodeComment).CommentAnnotation
                    Case Annotations.BlockComment, Annotations.LineComment
                        blockList += New MarkdownBlock With {
                            .markdown = DirectCast(line, CodeComment).comment
                        }
                    Case Annotations.RegionStart
                        regionMark = True
                        start = DirectCast(line, CodeComment).span
                        regionName = DirectCast(line, CodeComment).comment.GetStackValue("""", """")
                    Case Annotations.EndRegion
                        regionMark = False
                        blockList += New RCodeBlock With {
                            .block = codeBlock.PopAll,
                            .region = New IntRange(
                                min:=start.line - 1,
                                max:=DirectCast(line, CodeComment).span.line - 1
                            ),
                            .text = rscript.GetByLineRange(.region),
                            .regionName = regionName
                        }
                End Select
            ElseIf regionMark Then
                codeBlock += line
            Else
                blockList += New RCodeBlock With {
                    .block = {line}
                }
            End If
        Next

        Return New Notebook With {
            .m_list = blockList
        }
    End Function
End Class
