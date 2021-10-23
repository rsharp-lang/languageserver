
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Scripting.MetaData

<Package("engine")>
Public Module Rscript

    Sub New()

    End Sub

    <ExportAPI("toHtml")>
    Public Function toHtml(nb As Notebook) As String
        Return New HtmlWriter().GetHtml(nb)
    End Function

    <ExportAPI("parse")>
    Public Function ParseScript(handle As String) As Notebook

    End Function

End Module
