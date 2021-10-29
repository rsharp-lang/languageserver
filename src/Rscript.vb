Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports Microsoft.VisualBasic.Text.Xml
Imports SMRUCC.Rsharp.Runtime
Imports R = SMRUCC.Rsharp.Runtime.Components.Rscript

<Package("engine")>
Public Module RscriptEngine

    Sub New()

    End Sub

    Const highlightjs As String = "https://unpkg.com/@highlightjs/cdn-assets@11.3.1/highlight.min.js"
    Const highlightTheme As String = "https://unpkg.com/@highlightjs/cdn-assets@11.3.1/styles/vs.min.css"

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="nb"></param>
    ''' <param name="style">
    ''' the css style text
    ''' </param>
    ''' <returns></returns>
    <ExportAPI("toHtml")>
    Public Function toHtml(nb As Notebook,
                           Optional style As String = Nothing,
                           Optional strict As Boolean = False,
                           Optional env As Environment = Nothing) As String

        Dim content As String = New HtmlWriter(strict).GetHtml(nb, env.globalEnvironment)

        If style.StringEmpty Then
            style = ""
        End If

        Return "<!DOCTYPE html>" & vbCrLf &
            sprintf(
                <html language="en-US">
                    <head>
                        <link rel="stylesheet" href=<%= highlightTheme %>/>

                        <style id='extend-builder-css-inline-css' type='text/css'>
                            %s
                        </style>

                        <script type="text/javascript" src=<%= highlightjs %>></script>
                    </head>
                    <body>
                        <div class="notebook">
                            %s
                        </div>

                        <script type="text/javascript">hljs.highlightAll();</script>
                    </body>
                </html>, style, content)
    End Function

    <ExportAPI("parse")>
    Public Function ParseScript(handle As String) As Notebook
        Return R.AutoHandleScript(handle).DoCall(AddressOf Notebook.fromRscript)
    End Function

End Module
