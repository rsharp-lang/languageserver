Imports Microsoft.VisualBasic.Language
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols
Imports SMRUCC.Rsharp.Runtime.Components

''' <summary>
''' A notebook data object which is parsed from the R# script.
''' </summary>
Public Class Notebook

    Public Property blocks As NoteBlock()

    Public Shared Function fromRscript(rscript As Rscript) As Notebook
        Dim lines As Expression() = Expression.ParseLines(rscript, keepsCommentLines:=True).ToArray
        Dim blockList As New List(Of NoteBlock)
        Dim regionMark As Boolean = False
        Dim codeBlock As New List(Of Expression)

        For Each line As Expression In lines
            If TypeOf line Is CodeComment Then
                Select Case DirectCast(line, CodeComment).CommentAnnotation
                    Case Annotations.BlockComment, Annotations.LineComment
                        blockList += New MarkdownBlock With {
                            .markdown = DirectCast(line, CodeComment).comment
                        }
                    Case Annotations.RegionStart
                        regionMark = True
                    Case Annotations.EndRegion
                        regionMark = False
                        blockList += New RCodeBlock With {
                            .block = codeBlock.ToArray
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
            .blocks = blockList.ToArray
        }
    End Function
End Class

Public MustInherit Class NoteBlock

End Class

Public Class MarkdownBlock : Inherits NoteBlock

    Public Property markdown As String

End Class

Public Class RCodeBlock : Inherits NoteBlock

    Public Property block As Expression()

End Class