#Region "Microsoft.VisualBasic::cfbe8ae3d5c63f2ee2cadaf49934de92, Rnotebook\src\Notebook.vb"

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

    ' Class Notebook
    ' 
    '     Properties: blocks
    ' 
    '     Function: CreateRscript, fromRscript
    ' 
    '     Sub: Add
    ' 
    ' /********************************************************************************/

#End Region

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
                        If regionMark Then
                            ' skip
                        Else
                            blockList += New MarkdownBlock With {
                                .markdown = DirectCast(line, CodeComment).comment
                            }
                        End If
                    Case Annotations.RegionStart
                        regionMark = True
                        start = DirectCast(line, CodeComment).span
                        regionName = DirectCast(line, CodeComment).comment.GetStackValue("""", """")
                    Case Annotations.EndRegion
                        regionMark = False
                        blockList += New RCodeBlock With {
                            .block = codeBlock.PopAll,
                            .region = New IntRange(
                                min:=start.line,
                                max:=DirectCast(line, CodeComment).span.line
                            ),
                            .text = rscript.GetByLineRange(.region),
                            .regionName = regionName
                        }.Trim
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

