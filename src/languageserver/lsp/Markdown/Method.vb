Imports System.Text
Imports SMRUCC.Rsharp.Development
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.Closure
Imports SMRUCC.Rsharp.Runtime.Interop

Namespace LspMarkdown

    Module Method

        Public Function BuildHoverMarkdown(value As Object, name As String) As String
            Dim sb As New StringBuilder

            If TypeOf value Is RMethodInfo Then
                Dim info = DirectCast(value, RMethodInfo)

                sb.AppendLine("### ").Append(info.name).AppendLine()
                If Not String.IsNullOrEmpty(info.namespace) Then
                    sb.AppendLine("**namespace:** " & info.namespace)
                    sb.AppendLine()
                End If

                If info.parameters IsNot Nothing AndAlso info.parameters.Length > 0 Then
                    sb.AppendLine("**Parameters:**")
                    For Each p As RMethodArgument In info.parameters
                        sb.Append("- `").Append(p.name).Append("`")
                        If p.type IsNot Nothing Then
                            sb.Append(" : ").Append(p.type.ToString)
                        End If
                        sb.AppendLine()
                    Next
                End If

                sb.AppendLine()
                sb.Append("**Returns:** ").Append(If(info.returns Is Nothing, "any", info.returns.ToString))
            ElseIf TypeOf value Is DeclareNewFunction Then
                ' reuse the existing markdown docs renderer for runtime R# functions
                Dim md = ConsoleMarkdownPrinter.getMarkdownDocs(DirectCast(value, DeclareNewFunction))
                If Not String.IsNullOrEmpty(md) Then
                    Return md
                End If
                sb.AppendLine("### ").Append(DirectCast(value, DeclareNewFunction).funcName).AppendLine()
            Else
                Return Nothing
            End If

            Return sb.ToString
        End Function
    End Module
End Namespace