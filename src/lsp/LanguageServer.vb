Imports System.Reflection
Imports Flute.Http
Imports Flute.Http.Core
Imports Flute.Http.Core.Message
Imports Flute.Http.FileSystem
Imports Microsoft.VisualBasic.ApplicationServices

Public Class LanguageServer : Implements IAppHandler

    ReadOnly vscode As FileSystem
    ReadOnly http As HttpSocket

    Sub New(port As Integer, Optional vscode_clr As String = Nothing)
        If vscode_clr.FileExists(ZERO_Nonexists:=True) Then
            vscode = New FileSystem(FindResource(Assembly.LoadFrom(vscode_clr.GetFullPath)))
        End If

        http = New HttpDriver().HttpMethod("get", Me).GetSocket(port)
    End Sub

    Private Shared Function FindResource(asm As Assembly) As IFileSystemEnvironment

    End Function

    Private Sub AppHandler(request As HttpRequest, response As HttpResponse) Implements IAppHandler.AppHandler

    End Sub

    Public Sub Listen()
        Call http.Run()
    End Sub
End Class
