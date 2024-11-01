Imports System.IO
Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports Flute.Http
Imports Flute.Http.Core
Imports Flute.Http.Core.Message
Imports Flute.Http.FileSystem
Imports Microsoft.VisualBasic.ApplicationServices
Imports Microsoft.VisualBasic.ComponentModel.DataSourceModel
Imports Microsoft.VisualBasic.Emit.Delegates
Imports Microsoft.VisualBasic.MIME.text.markdown
Imports SMRUCC.Rsharp.Development
Imports SMRUCC.Rsharp.Interpreter
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.Closure
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Interop

Public Class LanguageServer : Implements IAppHandler

    ReadOnly vscode As FileSystem
    ReadOnly http As HttpSocket
    ReadOnly cache As New Dictionary(Of String, String)
    ReadOnly R As RInterpreter

    Sub New(port As Integer, Optional vscode_clr As String = Nothing)
        If vscode_clr.DirectoryExists Then
            vscode = New FileSystem(vscode_clr)
        ElseIf vscode_clr.FileExists(ZERO_Nonexists:=True) Then
            vscode = New FileSystem(FindResource(Assembly.LoadFrom(vscode_clr.GetFullPath)))
        End If

        R = RInterpreter.Rsharp
        R.RedirectOutput(New StreamWriter(Console.OpenStandardOutput), OutputEnvironments.Html)
        http = New HttpDriver() _
            .HttpMethod("get", Me) _
            .HttpMethod("post", AddressOf setDefault) _
            .GetSocket(port)
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

    ''' <summary>
    ''' http get &amp; host static files
    ''' </summary>
    ''' <param name="request"></param>
    ''' <param name="response"></param>
    Private Sub getDefault(request As HttpRequest, response As HttpResponse) Implements IAppHandler.AppHandler
        Dim url = Strings.Trim(request.URL.path).ToLower

        Select Case url
            Case "", "/", "/index.html", "/index.txt", "/index.htm"
                Dim bytes = vscode.GetByteBuffer("/index.html")

                Call response.WriteHeader("text/html", bytes.Length)
                Call response.Write(bytes)
            Case "/lsp/get/symbol/"
                Dim name = request.URL("symbol")
                Dim key = request.URL("key")
                Dim symbol As Object = R.Evaluate(name)
                Dim docs = R.globalEnvir _
                    .packages _
                    .packageDocs
                Dim markdown As New MarkdownRender

                Using s As New MemoryStream
                    R.globalEnvir.stdout.splitLogging(s)

                    If TypeOf symbol Is RMethodInfo Then
                        Call docs.PrintHelp(symbol, R.globalEnvir.stdout)
                    ElseIf TypeOf symbol Is DeclareNewFunction Then
                        Dim help = ConsoleMarkdownPrinter.getMarkdownDocs(DirectCast(symbol, DeclareNewFunction))

                        ' print the runtime function code
                        Call R.globalEnvir.stdout.WriteLine(markdown.Transform(help))
                        Call R.globalEnvir.stdout.WriteLine(DirectCast(symbol, DeclareNewFunction).ToString)
                    End If

                    Call R.globalEnvir.stdout.Flush()
                    Call s.Flush()
                    Call s.Seek(Scan0, SeekOrigin.Begin)
                    Call response.WriteHeader("text/html", s.Length)
                    Call response.Write(s.ToArray)
                End Using
            Case Else
                Call WebFileSystemListener.HostStaticFile(vscode, request, response)
        End Select
    End Sub

    Private Sub setDefault(request As HttpRequest, response As HttpResponse)
        Dim url = Strings.Trim(request.URL.path).ToLower
        Dim post = DirectCast(request, HttpPOSTRequest).POSTData
        Dim key As String = request.URL("key")

        Select Case url
            Case "/lsp/put/"
                SyncLock cache
                    cache(key) = post.Objects("doc")
                End SyncLock
            Case "/lsp/save/"
                cache(key).SaveTo(path:=post.Objects("file"))
        End Select
    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Sub Listen()
        Call http.Run()
    End Sub
End Class
