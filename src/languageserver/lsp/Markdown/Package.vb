Imports System.Text
Imports SMRUCC.Rsharp.Development.Package

Namespace LspMarkdown

    Module Package

        Public Function BuildHoverMarkdown(pkg As PackageInfo) As String
            Dim sb As New StringBuilder($"# Package: {pkg.namespace}")

            Call sb.AppendLine()
            Call sb.AppendLine()
            Call sb.AppendLine(pkg.description & vbCrLf)
            Call sb.AppendLine()
            Call sb.AppendLine()
            Call sb.AppendLine("url: " & pkg.url & vbCrLf)
            Call sb.AppendLine("author: " & pkg.publisher & vbCrLf)
            Call sb.AppendLine("revision: " & pkg.revision & vbCrLf)
            Call sb.AppendLine("cites: " & pkg.cites & vbCrLf)
            Call sb.AppendLine("category: " & pkg.category.Description & vbCrLf)

            Call sb.AppendLine($"## Symbol List")
            Call sb.AppendLine()
            Call sb.AppendLine(pkg.symbols.JoinBy(", "))

            Return sb.ToString
        End Function
    End Module
End Namespace