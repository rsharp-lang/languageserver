Imports System.Reflection
Imports Flute.Http
Imports Flute.Http.Core
Imports Flute.Http.Core.Message
Imports Flute.Http.FileSystem
Imports Microsoft.VisualBasic.ApplicationServices
Imports Microsoft.VisualBasic.ComponentModel.DataSourceModel
Imports Microsoft.VisualBasic.Emit.Delegates

Public Class LanguageServer : Implements IAppHandler

    ReadOnly vscode As FileSystem
    ReadOnly http As HttpSocket

    Sub New(port As Integer, Optional vscode_clr As String = Nothing)
        If vscode_clr.DirectoryExists Then
            vscode = New FileSystem(vscode_clr)
        ElseIf vscode_clr.FileExists(ZERO_Nonexists:=True) Then
            vscode = New FileSystem(FindResource(Assembly.LoadFrom(vscode_clr.GetFullPath)))
        End If

        http = New HttpDriver().HttpMethod("get", Me).GetSocket(port)
    End Sub

    Private Shared Function FindResource(asm As Assembly) As IFileSystemEnvironment
        Dim types As Type() = asm.GetTypes

        For Each t_module As Type In types
            Dim ptr As MethodInfo() = t_module.GetMethods(PublicShared)

            For Each f As MethodInfo In ptr
                If Not f.GetParameters.IsNullOrEmpty Then
                    Continue For
                End If
                If Not f.GetGenericArguments.IsNullOrEmpty Then
                    Continue For
                End If

                If f.ReturnType.ImplementInterface(Of IFileSystemEnvironment)() Then
                    Return f.Invoke(Nothing, {})
                End If
            Next
        Next

        Throw New InvalidProgramException("no resource was provided!")
    End Function

    Private Sub AppHandler(request As HttpRequest, response As HttpResponse) Implements IAppHandler.AppHandler
        Dim url = Strings.Trim(request.URL.path).ToLower

        Select Case url
            Case "", "/", "/index.html", "/index.txt", "/index.htm"
                Dim bytes = vscode.GetByteBuffer("/index.html")

                Call response.WriteHeader("text/html", bytes.Length)
                Call response.Write(bytes)
            Case Else
                If url.StartsWith("/get") Then
                Else
                    Call WebFileSystemListener.HostStaticFile(vscode, request, response)
                End If
        End Select
    End Sub

    Public Sub Listen()
        Call http.Run()
    End Sub
End Class
