Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports Microsoft.VisualBasic.Text.Xml
Imports R = SMRUCC.Rsharp.Runtime.Components.Rscript

<Package("engine")>
Public Module RscriptEngine

    Sub New()

    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="nb"></param>
    ''' <param name="style">
    ''' the css style text
    ''' </param>
    ''' <returns></returns>
    <ExportAPI("toHtml")>
    Public Function toHtml(nb As Notebook, Optional style As String = Nothing) As String
        Dim content As String = New HtmlWriter().GetHtml(nb)

        If style.StringEmpty Then
            style = ""
        End If

        Return "<!DOCTYPE html>" & vbCrLf &
            sprintf(
                <html language="en-US">
                    <head>
                        <style id='extend-builder-css-inline-css' type='text/css'>
                            %s
                        </style>
                    </head>
                    <body>
                        <div class="notebook">
                            %s
                        </div>
                    </body>
                </html>, style, content)
    End Function

    <ExportAPI("parse")>
    Public Function ParseScript(handle As String) As Notebook
        Return R.AutoHandleScript(handle).DoCall(AddressOf Notebook.fromRscript)
    End Function

End Module
