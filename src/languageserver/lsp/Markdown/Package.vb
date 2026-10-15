Imports System.Text
Imports SMRUCC.Rsharp.Development.Package

Namespace LspMarkdown

    Module Package

        Public Function BuildHoverMarkdown(pkg As PackageInfo) As String
            Dim sb As New StringBuilder($"Package: {pkg.namespace}")



            Return sb.ToString
        End Function
    End Module
End Namespace