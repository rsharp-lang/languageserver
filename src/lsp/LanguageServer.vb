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
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.Closure
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Components
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
        R.LoadLibrary("REnv", ignoreMissingStartupPackages:=True)
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

                Dim valType As Type = f.ReturnType
                Dim checkIFs As Boolean = valType.ImplementInterface(Of IFileSystemEnvironment)()

                If checkIFs OrElse valType Is GetType(IFileSystemEnvironment) Then
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
re0:
                    If TypeOf symbol Is RMethodInfo Then
                        Call docs.PrintHelp(symbol, R.globalEnvir.stdout)
                    ElseIf TypeOf symbol Is DeclareNewFunction Then
                        Dim help = ConsoleMarkdownPrinter.getMarkdownDocs(DirectCast(symbol, DeclareNewFunction))

                        ' print the runtime function code
                        Call R.globalEnvir.stdout.WriteLine(markdown.Transform(help))
                        ' Call R.globalEnvir.stdout.WriteLine(DirectCast(symbol, DeclareNewFunction).ToString)
                    Else
                        Dim f = R.globalEnvir.FindFunction(name)

                        If Not f Is Nothing Then
                            If TypeOf f.value Is RMethodInfo OrElse TypeOf f.value Is DeclareNewFunction Then
                                symbol = f.value
                                GoTo re0
                            End If
                        End If
                    End If

                    Call R.globalEnvir.stdout.Flush()
                    Call s.Flush()
                    Call s.Seek(Scan0, SeekOrigin.Begin)
                    Call response.WriteHeader("text/html", s.Length)
                    Call response.Write(s.ToArray)
                End Using
            Case "/lsp/get/functions/"
                Dim functions = R.globalEnvir.EnumerateAllFunctions.ToArray
                Dim names As String() = functions _
                    .Select(Function(f) f.name) _
                    .Distinct _
                    .ToArray

                Call response.WriteJSON(names)
            Case "/lsp/read/"
                Dim path As String = request.URL("file").UrlDecode
                Dim text As String = path.ReadAllText(throwEx:=False, suppress:=True)

                Call response.WriteHTML(text)
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
                Dim rscript_str As String = post.Objects("doc")

                SyncLock cache
                    cache(key) = rscript_str
                End SyncLock

                Dim parse = Program.BuildProgram(rscript_str)

                If Not parse Is Nothing Then
                    For Each line As Expression In parse
                        If TypeOf line Is Require OrElse TypeOf line Is DeclareNewFunction Then
                            Call R.Evaluate(line)
                        ElseIf TypeOf line Is DeclareLambdaFunction Then
                            With DirectCast(line, DeclareLambdaFunction)
                                R.globalEnvir.funcSymbols(.name) = New Symbol(.name, line)
                            End With
                        End If
                    Next
                End If
            Case "/lsp/save/"
                cache(key).SaveTo(path:=post.Objects("file"))
        End Select

        Call response.WriteHTML("ok")
    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Sub Listen()
        Call http.Run()
    End Sub
End Class
