
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports R = SMRUCC.Rsharp.Runtime.Components.Rscript

<Package("engine")>
Public Module RscriptEngine

    Sub New()

    End Sub

    <ExportAPI("toHtml")>
    Public Function toHtml(nb As Notebook) As String
        Return New HtmlWriter().GetHtml(nb)
    End Function

    <ExportAPI("parse")>
    Public Function ParseScript(handle As String) As Notebook
        Return R.AutoHandleScript(handle).DoCall(AddressOf Notebook.fromRscript)
    End Function

End Module
