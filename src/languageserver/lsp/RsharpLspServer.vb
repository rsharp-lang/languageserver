Imports System.IO
Imports System.Text
Imports Microsoft.VisualBasic.ComponentModel.DataSourceModel
Imports Microsoft.VisualBasic.MIME.application.json
Imports Microsoft.VisualBasic.MIME.application.json.Javascript
Imports SMRUCC.Rsharp.Development
Imports SMRUCC.Rsharp.Development.CodeAnalysis
Imports SMRUCC.Rsharp.Development.Package
Imports SMRUCC.Rsharp.Interpreter
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.Closure
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.DataSets
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.Operators
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Components
Imports SMRUCC.Rsharp.Runtime.Components.Interface
Imports SMRUCC.Rsharp.Runtime.Interop
Imports jsobj = Microsoft.VisualBasic.MIME.application.json.Javascript.JsonObject
Imports jsVal = Microsoft.VisualBasic.MIME.application.json.Javascript.JsonValue
Imports rlang = SMRUCC.Rsharp.Language

''' <summary>
''' An implementation of the Language Server Protocol for the R# scripting language.
''' The server communicates with the editor (e.g. VS Code) over stdin/stdout using
''' the Content-Length framed JSON-RPC transport from the sciBASIC# JsonRpc module.
''' All log/trace output is written to stderr so that the stdout stream stays clean
''' for the LSP message frames.
''' </summary>
Public Class RsharpLspServer

    ''' <summary>LSP symbol kind: Function</summary>
    Private Const SYM_Function As Integer = 12
    ''' <summary>LSP completion item kind: Function</summary>
    Private Const CI_Function As Integer = 3
    ''' <summary>LSP completion item kind: Variable</summary>
    Private Const CI_Variable As Integer = 6
    ''' <summary>LSP completion item kind: Keyword</summary>
    Private Const CI_Keyword As Integer = 14

    Private Shared ReadOnly RKeywords As String() = {
        "if", "else", "elseif", "for", "while", "repeat", "next", "break",
        "function", "lambda", "return", "require", "imports", "let", "const",
        "TRUE", "FALSE", "NULL", "NA", "NaN", "Inf",
        "in", "with", "using", "try", "catch", "finally", "throw",
        "switch", "case", "do", "global", "env", "options", "foreach"
    }

    ReadOnly R As RInterpreter
    ReadOnly trace As TextWriter
    ReadOnly docs As New Dictionary(Of String, String)
    ReadOnly globalFunctions As New List(Of NamedValue(Of String))

    Private stdin As Stream
    Private stdout As Stream
    Private shutdownRequested As Boolean = False

    ''' <summary>
    ''' the transport streams injected by an in-process host. when both are
    ''' <c>Nothing</c> the server falls back to the process stdin/stdout, which is
    ''' the stdio mode used by the lsp.vbproj command line host.
    ''' </summary>
    Private ReadOnly inputOverride As Stream
    Private ReadOnly outputOverride As Stream

    ''' <summary>
    ''' stdio mode (default): the server reads the process stdin and writes the
    ''' process stdout. this is the mode used by the lsp.vbproj command line host.
    ''' </summary>
    Sub New(Optional R As RInterpreter = Nothing, Optional trace As TextWriter = Nothing)
        Me.New(Nothing, Nothing, R, trace)
    End Sub

    ''' <summary>
    ''' in-process mode: the caller may inject the transport streams (pass
    ''' <c>Nothing</c> to fall back to the console) and, more importantly, drive the
    ''' server directly through <see cref="HandleMessageAsync"/> without ever
    ''' running the blocking stdio read loop.
    ''' </summary>
    ''' <param name="input">the stream the read loop consumes, or Nothing for the process stdin.</param>
    ''' <param name="output">the stream responses are written to, or Nothing for the process stdout.</param>
    ''' <param name="trace">the log sink; Nothing falls back to the process stderr.</param>
    Sub New(input As Stream, output As Stream, Optional R As RInterpreter = Nothing, Optional trace As TextWriter = Nothing)
        Me.inputOverride = input
        Me.outputOverride = output
        Me.trace = If(trace, Console.Error)
        Me.R = If(R, RInterpreter.Rsharp)

        ' redirect the R# interpreter output to stderr so it never pollutes the
        ' LSP message frames that travel on stdout. a GUI host has no console
        ' attached, so opening the standard error stream may fail: it is only a
        ' diagnostic channel and must never take the server down.
        Try
            R.RedirectOutput(New StreamWriter(Console.OpenStandardError, leaveOpen:=True) With {.AutoFlush = True}, OutputEnvironments.Console)
        Catch ex As Exception
            Me.trace.WriteLine("[rsharp-lsp] RedirectOutput warning: " & ex.Message)
        End Try

        Try
            R.LoadLibrary("REnv", ignoreMissingStartupPackages:=True)
        Catch ex As Exception
            Me.trace.WriteLine("[rsharp-lsp] LoadLibrary REnv warning: " & ex.Message)
        End Try

        CacheGlobalFunctions()
        Me.trace.WriteLine($"[rsharp-lsp] loaded {globalFunctions.Count} global functions")
    End Sub

    Private Sub CacheGlobalFunctions()
        globalFunctions.Clear()

        Try
            Dim seen As New HashSet(Of String)

            For Each sym As Symbol In R.globalEnvir.EnumerateAllFunctions
                Dim nm As String = sym.name

                If String.IsNullOrEmpty(nm) OrElse seen.Contains(nm) Then
                    Continue For
                End If

                seen.Add(nm)

                Dim ns As String = ""

                If TypeOf sym.value Is RMethodInfo Then
                    ns = DirectCast(sym.value, RMethodInfo).namespace
                End If

                globalFunctions.Add(New NamedValue(Of String)(nm, ns))
            Next
        Catch ex As Exception
            Me.trace.WriteLine("[rsharp-lsp] EnumerateAllFunctions error: " & ex.Message)
        End Try
    End Sub

    ' ------------------------------------------------------------------
    ' main loop
    ' ------------------------------------------------------------------

    ''' <summary>
    ''' run the json-rpc message loop until the peer disconnects or asks to exit.
    ''' </summary>
    Public Async Function RunAsync() As Task
        ' the injected streams win; stdio is only the fallback for the CLI host
        stdin = If(inputOverride, New BomStrippingStream(Console.OpenStandardInput))
        stdout = If(outputOverride, Console.OpenStandardOutput)

        Me.trace.WriteLine("[rsharp-lsp] server started, listening on stdio")

        While Not shutdownRequested
            Dim msg As jsobj = Await JsonRpc.ReadMessageAsync(stdin)

            If msg Is Nothing Then
                Exit While
            End If

            Try
                Await Dispatch(msg)
            Catch ex As Exception
                Me.trace.WriteLine("[rsharp-lsp] dispatch error: " & ex.Message)
            End Try
        End While

        Me.trace.WriteLine("[rsharp-lsp] server stopped")
    End Function

    ''' <summary>
    ''' handle a single json-rpc message and return the response object to send
    ''' back, or <c>Nothing</c> for notifications / messages that need no reply.
    ''' this is the transport agnostic entry point shared by the stdio read loop
    ''' (<see cref="RunAsync"/>) and by an in-process host (such as the WebView2 /
    ''' Monaco editor) that drives the server message by message.
    ''' </summary>
    Public Function HandleMessageAsync(msg As jsobj) As Task(Of jsobj)
        Dim method As String = msg.Str("method")
        Dim id As JsonElement = msg("id")
        Dim isRequest As Boolean = Not (id Is Nothing)
        Dim result As JsonElement = Nothing
        Dim failCode As Integer = 0
        Dim failMessage As String = Nothing

        Me.trace.WriteLine($"[rsharp-lsp] <= {method} request={isRequest}")

        Select Case method
            Case "initialize"
                result = OnInitialize(DirectCast(msg("params"), jsobj))
            Case "initialized"
                ' notification, nothing to reply
            Case "shutdown"
                ' must reply with a null result
                result = Nothing
            Case "exit"
                shutdownRequested = True
            Case "textDocument/didOpen"
                OnDidOpen(DirectCast(msg("params"), jsobj))
            Case "textDocument/didChange"
                OnDidChange(DirectCast(msg("params"), jsobj))
            Case "textDocument/didClose"
                OnDidClose(DirectCast(msg("params"), jsobj))
            Case "textDocument/hover"
                result = OnHover(DirectCast(msg("params"), jsobj))
            Case "textDocument/completion"
                result = OnCompletion(DirectCast(msg("params"), jsobj))
            Case "textDocument/documentSymbol"
                result = OnDocumentSymbol(DirectCast(msg("params"), jsobj))
            Case "textDocument/definition"
                result = OnDefinition(DirectCast(msg("params"), jsobj))
            Case "textDocument/signatureHelp"
                result = OnSignatureHelp(DirectCast(msg("params"), jsobj))
            Case "workspace/symbol"
                result = OnWorkspaceSymbol(DirectCast(msg("params"), jsobj))
            Case Else
                If isRequest Then
                    failCode = -32601
                    failMessage = "Method not found: " & method
                End If
        End Select

        If Not isRequest Then
            ' notifications never get a response
            Return Task.FromResult(Of jsobj)(Nothing)
        ElseIf failMessage IsNot Nothing Then
            Return Task.FromResult(Of jsobj)(JsonRpc.MakeError(id, failCode, failMessage))
        Else
            Return Task.FromResult(Of jsobj)(JsonRpc.MakeResponse(id, result))
        End If
    End Function

    ''' <summary>
    ''' stdio driver: read one message at a time from <see cref="stdin"/>, let
    ''' <see cref="HandleMessageAsync"/> build the response and write it back to
    ''' <see cref="stdout"/>. an in-process host skips this method entirely.
    ''' </summary>
    Private Async Function Dispatch(msg As jsobj) As Task
        Dim method As String = msg.Str("method")
        Dim response As jsobj = Await HandleMessageAsync(msg)

        If response IsNot Nothing Then
            Await JsonRpc.SendMessageAsync(stdout, response)
            Me.trace.WriteLine($"[rsharp-lsp] => {method} sent")
        End If
    End Function

    ' ------------------------------------------------------------------
    ' document cache
    ' ------------------------------------------------------------------

    Private Function GetDoc(uri As String) As String
        If docs.ContainsKey(uri) Then
            Return docs(uri)
        End If
        Return Nothing
    End Function

    Private Sub OnDidOpen(params As jsobj)
        If params Is Nothing Then Return
        Dim td = DirectCast(params("textDocument"), jsobj)
        Dim uri = TextOf(td("uri"))
        docs(uri) = TextOf(td("text"))
    End Sub

    Private Sub OnDidChange(params As jsobj)
        If params Is Nothing Then Return
        Dim td = DirectCast(params("textDocument"), jsobj)
        Dim uri = TextOf(td("uri"))
        ' textDocumentSync is configured as Full (1): the first change item carries
        ' the complete document text
        Dim changes = params("contentChanges")

        If TypeOf changes Is JsonArray Then
            Dim arr = DirectCast(changes, JsonArray)
            If arr.Count > 0 Then
                docs(uri) = TextOf(DirectCast(arr(0), jsobj)("text"))
                Return
            End If
        End If

        docs(uri) = TextOf(td("text"))
    End Sub

    Private Sub OnDidClose(params As jsobj)
        If params Is Nothing Then Return
        Dim td = DirectCast(params("textDocument"), jsobj)
        Dim uri = TextOf(td("uri"))
        If docs.ContainsKey(uri) Then docs.Remove(uri)
    End Sub

    ' ------------------------------------------------------------------
    ' lifecycle / capabilities
    ' ------------------------------------------------------------------

    Private Function OnInitialize(params As jsobj) As jsobj
        Dim textDoc = New jsobj()

        textDoc("hoverProvider") = New jsVal(True)

        Dim comp = New jsobj()
        comp("triggerCharacters") = New JsonArray(New jsVal("("c), New jsVal("."c), New jsVal(" "c))
        comp("resolveProvider") = New jsVal(False)
        textDoc("completionProvider") = comp

        textDoc("documentSymbolProvider") = New jsVal(True)
        textDoc("definitionProvider") = New jsVal(True)
        textDoc("workspaceSymbolProvider") = New jsVal(True)

        Dim sig = New jsobj()
        sig("triggerCharacters") = New JsonArray(New jsVal("("c), New jsVal(","c))
        textDoc("signatureHelpProvider") = sig

        ' Full document sync: the client sends the entire document on every change
        textDoc("textDocumentSync") = New jsVal(1)

        Dim serverInfo = New jsobj()
        serverInfo("name") = New jsVal("rsharp-lsp")
        serverInfo("version") = New jsVal("1.0.0")

        Dim result = New jsobj()
        result("capabilities") = textDoc
        result("serverInfo") = serverInfo

        Return result
    End Function

    ' ------------------------------------------------------------------
    ' hover
    ' ------------------------------------------------------------------

    Private Function OnHover(params As jsobj) As jsobj
        If params Is Nothing Then Return Nothing

        Dim uri = TextOf(DirectCast(params("textDocument"), jsobj)("uri"))
        Dim line = params.Int("position", "line")
        Dim col = params.Int("position", "character")
        Dim text = GetDoc(uri)

        If text Is Nothing Then
            Return Nothing
        End If

        Dim word = GetWordAt(text, line, col)

        If String.IsNullOrEmpty(word) Then
            Return Nothing
        End If

        Dim name = SimpleName(word)
        Dim sym = R.globalEnvir.FindFunction(name)
        Dim md As String = Nothing

        If sym Is Nothing Then
            sym = R.globalEnvir.getPrimitive(name)
        End If

        If sym IsNot Nothing Then
            md = LspMarkdown.Method.BuildHoverMarkdown(sym.value, name, R.globalEnvir)
        ElseIf rlang.KeywordSet.ContainsKey(name) Then
            md = rlang.KeywordSet(name)
        Else
            Dim pkg As PackageInfo = R.globalEnvir.packages.packageRepository.FindPackageInfo(name)

            If pkg IsNot Nothing Then
                md = LspMarkdown.Package.BuildHoverMarkdown(pkg)
            End If
        End If

        If String.IsNullOrEmpty(md) Then
            ' the global environment only exposes the built-in / package functions,
            ' so a function declared in the open document needs a local lookup
            Dim localFunc = FindDocumentFunction(text, name)

            If localFunc IsNot Nothing Then
                md = LspMarkdown.Method.BuildHoverMarkdown(localFunc, name, R.globalEnvir)
            End If
        End If

        If String.IsNullOrEmpty(md) Then
            Return Nothing
        End If

        Dim contents = New jsobj()
        contents("kind") = New jsVal("markdown")
        contents("value") = New jsVal(md)

        Dim result = New jsobj()
        result("contents") = contents

        Return result
    End Function

    ' ------------------------------------------------------------------
    ' completion
    ' ------------------------------------------------------------------

    Private Function OnCompletion(params As jsobj) As JsonElement
        If params Is Nothing Then Return New JsonArray
        Dim uri = TextOf(DirectCast(params("textDocument"), jsobj)("uri"))
        Dim line = params.Int("position", "line")
        Dim col = params.Int("position", "character")
        Dim text = GetDoc(uri)

        Dim prefix As String = If(text Is Nothing, "", GetPrefixAt(text, line, col))

        Dim items = New JsonArray()
        Dim globalNames As New HashSet(Of String)

        For Each f As NamedValue(Of String) In globalFunctions
            If MatchesPrefix(f.Name, prefix) Then
                items.Add(CompletionItem(f.Name, CI_Function, If(String.IsNullOrEmpty(f.Value), Nothing, f.Value)))
                globalNames.Add(f.Name)
            End If

            If items.Count >= 500 Then Exit For
        Next

        ' language keywords (skip ones already provided as global functions)
        For Each kw In RKeywords
            If globalNames.Contains(kw) Then Continue For
            If MatchesPrefix(kw, prefix) Then
                items.Add(CompletionItem(kw, CI_Keyword, Nothing))
            End If
        Next

        ' symbols declared / referenced inside the current document
        If text IsNot Nothing Then
            For Each sym In GetDocSymbolNames(text)
                If globalNames.Contains(sym) Then Continue For
                If MatchesPrefix(sym, prefix) Then
                    items.Add(CompletionItem(sym, CI_Variable, Nothing))
                End If
            Next
        End If

        Return items
    End Function

    Private Iterator Function GetDocSymbolNames(text As String) As IEnumerable(Of String)
        Dim prog = Program.BuildProgram(text)

        If prog Is Nothing Then Return

        Dim seen As New HashSet(Of String)

        For Each line As Expression In prog
            For Each ref In SymbolAnalysis.GetSymbolReferenceList(line)
                If Not seen.Contains(ref.Name) Then
                    seen.Add(ref.Name)
                    Yield ref.Name
                End If
            Next
        Next
    End Function

    Private Shared Function CompletionItem(label As String, kind As Integer, detail As String) As jsobj
        Dim it = New jsobj()
        it("label") = New jsVal(label)
        it("kind") = New jsVal(kind)
        If Not String.IsNullOrEmpty(detail) Then
            it("detail") = New jsVal(detail)
        End If
        Return it
    End Function

    ' ------------------------------------------------------------------
    ' document symbols
    ' ------------------------------------------------------------------

    Private Function OnDocumentSymbol(params As jsobj) As JsonElement
        If params Is Nothing Then Return New JsonArray
        Dim uri = TextOf(DirectCast(params("textDocument"), jsobj)("uri"))
        Dim text = GetDoc(uri)
        Dim arr = New JsonArray()

        If text Is Nothing Then Return arr

        Dim prog = Program.BuildProgram(text)
        If prog Is Nothing Then Return arr

        For Each fn In EnumerateTopLevelFunctions(prog)
            Call R.Set(fn.Name, fn.Value)
            Call arr.Add(MakeDocumentSymbol(fn.Name, fn.Value, text))
        Next

        Return arr
    End Function

    ''' <summary>
    ''' collect the top-level function declarations in a script. covers both the
    ''' declaration form <c>function name() {}</c> and the assignment form
    ''' <c>name &lt;- function() {}</c> (a DeclareNewSymbol whose value is a function).
    ''' </summary>
    Private Function EnumerateTopLevelFunctions(prog As Program) As List(Of NamedValue(Of Expression))
        Dim list As New List(Of NamedValue(Of Expression))

        For Each line As Expression In prog
            Dim name As String = Nothing
            Dim fnNode As Expression = GetFunctionNode(line)

            If TypeOf line Is DeclareNewFunction Then
                name = NormalizeSymbolName(DirectCast(line, DeclareNewFunction).funcName)
                fnNode = line
            ElseIf TypeOf line Is DeclareLambdaFunction Then
                name = NormalizeSymbolName(DirectCast(line, DeclareLambdaFunction).name)
                fnNode = line
            ElseIf TypeOf line Is DeclareNewSymbol Then
                Dim ds = DirectCast(line, DeclareNewSymbol)
                If ds.names IsNot Nothing AndAlso ds.names.Count > 0 AndAlso IsFunctionExpr(ds.value) Then
                    name = NormalizeSymbolName(ds.names(0))
                End If
            ElseIf TypeOf line Is ValueAssignExpression Then
                Dim va = DirectCast(line, ValueAssignExpression)
                If va.targetSymbols IsNot Nothing AndAlso IsFunctionExpr(va.value) Then
                    For Each nm In va.targetSymbols
                        name = NormalizeSymbolName(If(TypeOf nm Is SymbolReference, DirectCast(nm, SymbolReference).symbol, nm.ToString()))
                        Exit For
                    Next
                End If
            End If

            If Not String.IsNullOrEmpty(name) Then
                Dim rangeNode As Expression = If(TypeOf fnNode Is DeclareNewFunction OrElse TypeOf fnNode Is DeclareLambdaFunction, fnNode, line)
                list.Add(New NamedValue(Of Expression)(name, rangeNode))
            End If
        Next

        Return list
    End Function

    ''' <summary>
    ''' find the top level function declared in the open document that matches
    ''' <paramref name="name"/>, or <c>Nothing</c> when the document does not declare
    ''' it. the global environment only knows the built-in / package functions of the
    ''' interpreter, so a document local lookup is required before giving up.
    ''' </summary>
    Private Function FindDocumentFunction(text As String, name As String) As DeclareNewFunction
        If text Is Nothing OrElse String.IsNullOrEmpty(name) Then Return Nothing

        Dim prog = Program.BuildProgram(text)

        If prog Is Nothing Then Return Nothing

        For Each fn In EnumerateTopLevelFunctions(prog)
            If fn.Name = name Then
                Return TryCast(GetFunctionNode(fn.Value), DeclareNewFunction)
            End If
        Next

        Return Nothing
    End Function

    ''' <summary>
    ''' normalize a symbol name extracted from the ast. R# renders some expression
    ''' nodes through ToString(), which wraps the identifier in quotes (a degraded
    ''' parse yields the literal text <c>"add"</c> instead of the identifier
    ''' <c>add</c>); strip the wrapping quotes so the name stays usable as a
    ''' completion / document symbol / definition key.
    ''' </summary>
    Private Shared Function NormalizeSymbolName(value As String) As String
        If String.IsNullOrEmpty(value) Then Return value

        Dim name As String = value.Trim()

        If name.Length >= 2 Then
            Dim head As Char = name(0)
            Dim tail As Char = name(name.Length - 1)

            If (head = """"c AndAlso tail = """"c) OrElse (head = "'"c AndAlso tail = "'"c) Then
                Return name.Substring(1, name.Length - 2).Trim()
            End If
        End If

        Return name
    End Function

    ''' <summary>
    ''' get the actual function declaration node (DeclareNewFunction / DeclareLambdaFunction)
    ''' for an expression, descending through assignment wrappers if needed.
    ''' </summary>
    Private Shared Function GetFunctionNode(expr As Expression) As Expression
        If TypeOf expr Is DeclareNewFunction OrElse TypeOf expr Is DeclareLambdaFunction Then
            Return expr
        End If
        If TypeOf expr Is ValueAssignExpression Then
            Return GetFunctionNode(DirectCast(expr, ValueAssignExpression).value)
        End If
        If TypeOf expr Is DeclareNewSymbol Then
            Return GetFunctionNode(DirectCast(expr, DeclareNewSymbol).value)
        End If
        Return expr
    End Function

    Private Shared Function IsFunctionExpr(val As Expression) As Boolean
        If val Is Nothing Then Return False
        If TypeOf val Is DeclareNewFunction OrElse TypeOf val Is DeclareLambdaFunction Then
            Return True
        End If
        If TypeOf val Is ValueAssignExpression Then
            Return IsFunctionExpr(DirectCast(val, ValueAssignExpression).value)
        End If
        If TypeOf val Is DeclareNewSymbol Then
            Return IsFunctionExpr(DirectCast(val, DeclareNewSymbol).value)
        End If
        Return False
    End Function

    Private Function MakeDocumentSymbol(name As String, code As Expression, text As String) As jsobj
        Dim lineNo = ParseLine(DirectCast(code, IRuntimeTrace).stackFrame?.Line)
        Dim rng = LineRange(text, lineNo)

        Dim ds = New jsobj()
        ds("name") = New jsVal(name)
        ds("kind") = New jsVal(SYM_Function)
        ds("range") = rng
        ds("selectionRange") = rng

        Return ds
    End Function

    ' ------------------------------------------------------------------
    ' go to definition
    ' ------------------------------------------------------------------

    Private Function OnDefinition(params As jsobj) As JsonElement
        If params Is Nothing Then Return Nothing
        Dim uri = TextOf(DirectCast(params("textDocument"), jsobj)("uri"))
        Dim line = params.Int("position", "line")
        Dim col = params.Int("position", "character")
        Dim text = GetDoc(uri)

        If text Is Nothing Then Return Nothing

        Dim word = GetWordAt(text, line, col)
        If String.IsNullOrEmpty(word) Then Return Nothing

        Dim name = SimpleName(word)
        Dim prog = Program.BuildProgram(text)

        If prog Is Nothing Then Return Nothing

        For Each fn In EnumerateTopLevelFunctions(prog)
            If fn.Name = name Then
                Return LocationArray(uri, fn.Value)
            End If
        Next

        Return Nothing
    End Function

    Private Function LocationArray(uri As String, code As Expression) As JsonArray
        Dim arr = New JsonArray()
        arr.Add(MakeLocation(uri, code))
        Return arr
    End Function

    Private Function MakeLocation(uri As String, code As Expression) As jsobj
        Dim lineNo = ParseLine(DirectCast(code, IRuntimeTrace).stackFrame?.Line)
        Dim loc = New jsobj()
        loc("uri") = New jsVal(uri)
        loc("range") = LineRange(GetDoc(uri), lineNo)
        Return loc
    End Function

    ' ------------------------------------------------------------------
    ' workspace symbols
    ' ------------------------------------------------------------------

    Private Function OnWorkspaceSymbol(params As jsobj) As JsonElement
        If params Is Nothing Then Return New JsonArray
        Dim query = TextOf(params("query"))
        Dim arr = New JsonArray()
        Dim pseudoUri = FirstOpenUri()

        For Each f As NamedValue(Of String) In globalFunctions
            If String.IsNullOrEmpty(query) OrElse f.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 Then
                Dim si = New jsobj()
                si("name") = New jsVal(f.Name)
                si("kind") = New jsVal(SYM_Function)
                If Not String.IsNullOrEmpty(f.Value) Then
                    si("containerName") = New jsVal(f.Value)
                End If
                si("location") = ZeroLocation(pseudoUri)
                arr.Add(si)
            End If

            If arr.Count >= 500 Then Exit For
        Next

        Return arr
    End Function

    Private Function FirstOpenUri() As String
        If docs.Count > 0 Then
            Return docs.Keys(0)
        End If
        Return "file:///RsharpGlobal"
    End Function

    ' ------------------------------------------------------------------
    ' signature help
    ' ------------------------------------------------------------------

    Private Function OnSignatureHelp(params As jsobj) As jsobj
        If params Is Nothing Then Return Nothing
        Dim uri = TextOf(DirectCast(params("textDocument"), jsobj)("uri"))
        Dim line = params.Int("position", "line")
        Dim col = params.Int("position", "character")
        Dim text = GetDoc(uri)

        If text Is Nothing Then Return Nothing

        Dim callInfo = FindEnclosingCall(text, line, col)
        If String.IsNullOrEmpty(callInfo.funcName) Then Return Nothing

        Dim name = SimpleName(callInfo.funcName)
        Dim sym = R.globalEnvir.FindFunction(name)
        Dim value As Object = If(sym Is Nothing, Nothing, sym.value)
        Dim documentLocal As Boolean = False

        If value Is Nothing Then
            ' the global environment only exposes the built-in / package functions,
            ' so a function declared in the open document needs a local lookup
            value = FindDocumentFunction(text, name)
            documentLocal = value IsNot Nothing
        End If

        If value Is Nothing Then Return Nothing

        Dim label As String = Nothing
        Dim parameters = New JsonArray()

        If TypeOf value Is RMethodInfo Then
            Dim info = DirectCast(value, RMethodInfo)
            Dim parts As New List(Of String)

            If info.parameters IsNot Nothing Then
                For Each p As RMethodArgument In info.parameters
                    parts.Add(p.name)
                    Dim pi = New jsobj()
                    pi("label") = New jsVal(p.name)
                    If p.type IsNot Nothing Then
                        pi("documentation") = New jsVal(p.type.ToString)
                    End If
                    parameters.Add(pi)
                Next
            End If

            label = info.name & "(" & String.Join(", ", parts) & ")"
        ElseIf TypeOf value Is DeclareNewFunction Then
            Dim fn = DirectCast(value, DeclareNewFunction)
            Dim parts As New List(Of String)

            For Each d As DeclareNewSymbol In fn.parameters
                For Each n In d.names
                    parts.Add(n)
                    Dim pi = New jsobj()
                    pi("label") = New jsVal(n)
                    parameters.Add(pi)
                Next
            Next

            ' a document local function is reported with the identifier the user
            ' actually typed: its ast funcName may carry an anonymous id suffix
            Dim displayName As String = If(documentLocal, name, NormalizeSymbolName(fn.funcName))

            label = displayName & "(" & String.Join(", ", parts) & ")"
        Else
            Return Nothing
        End If

        Dim sig = New jsobj()
        sig("label") = New jsVal(label)
        sig("parameters") = parameters

        Dim signatures = New JsonArray()
        signatures.Add(sig)

        Dim result = New jsobj()
        result("signatures") = signatures
        result("activeSignature") = New jsVal(0)
        result("activeParameter") = New jsVal(callInfo.activeParam)

        Return result
    End Function

    ' ------------------------------------------------------------------
    ' helpers
    ' ------------------------------------------------------------------

    Private Shared Function TextOf(el As JsonElement) As String
        If el Is Nothing Then Return ""
        Return el.ToString
    End Function

    Private Shared Function MatchesPrefix(name As String, prefix As String) As Boolean
        If String.IsNullOrEmpty(prefix) Then Return True
        Return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function SimpleName(word As String) As String
        ' strip any namespace / member qualifier: "pkg::func" or "obj.func" -> "func"
        Dim i = word.LastIndexOf("::")
        If i >= 0 Then Return word.Substring(i + 2)
        i = word.LastIndexOf("."c)
        If i >= 0 Then Return word.Substring(i + 1)
        Return word
    End Function

    Private Shared Function IsIdentChar(c As Char) As Boolean
        Return Char.IsLetterOrDigit(c) OrElse c = "_"c OrElse c = "."c OrElse c = ":"c
    End Function

    ''' <summary>extract the full identifier surrounding the cursor (hover/definition).</summary>
    Private Shared Function GetWordAt(text As String, line As Integer, col As Integer) As String
        Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
        If line < 0 OrElse line >= lines.Length Then Return ""
        Dim s = lines(line)
        If col > s.Length Then col = s.Length

        Dim left = col
        While left > 0 AndAlso IsIdentChar(s(left - 1))
            left -= 1
        End While

        Dim right = col
        While right < s.Length AndAlso IsIdentChar(s(right))
            right += 1
        End While

        If right <= left Then Return ""
        Return s.Substring(left, right - left)
    End Function

    ''' <summary>extract the partial identifier immediately left of the cursor (completion).</summary>
    Private Shared Function GetPrefixAt(text As String, line As Integer, col As Integer) As String
        Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
        If line < 0 OrElse line >= lines.Length Then Return ""
        Dim s = lines(line)
        If col > s.Length Then col = s.Length

        Dim left = col
        While left > 0 AndAlso IsIdentChar(s(left - 1))
            left -= 1
        End While

        If left = col Then Return ""
        Return s.Substring(left, col - left)
    End Function

    Private Shared Function ParseLine(lineText As String) As Integer
        Dim n As Integer = 0
        If Integer.TryParse(lineText, n) AndAlso n > 0 Then
            Return n - 1
        End If
        Return 0
    End Function

    Private Shared Function LineRange(text As String, zeroBasedLine As Integer) As jsobj
        Dim len As Integer = 0
        If text IsNot Nothing Then
            Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
            If zeroBasedLine >= 0 AndAlso zeroBasedLine < lines.Length Then
                len = lines(zeroBasedLine).Length
            End If
        End If

        Dim start = New jsobj()
        start("line") = New jsVal(zeroBasedLine)
        start("character") = New jsVal(0)

        Dim [end] = New jsobj()
        [end]("line") = New jsVal(zeroBasedLine)
        [end]("character") = New jsVal(len)

        Dim rng = New jsobj()
        rng("start") = start
        rng("end") = [end]

        Return rng
    End Function

    ''' <summary>
    ''' a zero-length location at the document top, used for workspace symbols that
    ''' have no source file inside the workspace.
    ''' </summary>
    Private Shared Function ZeroLocation(uri As String) As jsobj
        Dim loc = New jsobj()
        loc("uri") = New jsVal(uri)
        loc("range") = LineRange(Nothing, 0)
        Return loc
    End Function

    ''' <summary>
    ''' find the enclosing function call around the cursor. returns the function name
    ''' and the zero-based index of the active parameter (count of top-level commas).
    ''' </summary>
    Private Shared Function FindEnclosingCall(text As String, line As Integer, col As Integer) As (funcName As String, activeParam As Integer)
        Dim lines = text.Split(New String() {vbCrLf, vbLf}, StringSplitOptions.None)
        Dim prefix As New StringBuilder

        For i = 0 To line - 1
            prefix.Append(lines(i)).Append(vbLf)
        Next
        If line < lines.Length Then
            prefix.Append(lines(line).Substring(0, Math.Min(col, lines(line).Length)))
        End If

        Dim s = prefix.ToString
        Dim depth As Integer = 0
        Dim openIndex As Integer = -1
        Dim commaCount As Integer = 0

        Dim i2 = s.Length - 1
        While i2 >= 0
            Dim c = s(i2)
            If c = ")"c Then
                depth += 1
            ElseIf c = "("c Then
                If depth = 0 Then
                    openIndex = i2
                    Exit While
                End If
                depth -= 1
            ElseIf c = ","c AndAlso depth = 0 Then
                commaCount += 1
            End If
            i2 -= 1
        End While

        If openIndex < 0 Then Return ("", 0)

        Dim k = openIndex - 1
        While k >= 0 AndAlso s(k) = " "c
            k -= 1
        End While

        Dim m = k
        While m >= 0 AndAlso IsIdentChar(s(m))
            m -= 1
        End While

        Dim name = s.Substring(m + 1, k - m)
        Return (name, commaCount)
    End Function

    ''' <summary>
    ''' a thin stream wrapper that transparently strips a single leading UTF-8 BOM
    ''' (EF BB BF) from the beginning of the stream, then forwards every other byte
    ''' unchanged. some clients / shells prepend a BOM to the stdio stream which would
    ''' otherwise break the Content-Length header parsing.
    ''' </summary>
    Private Class BomStrippingStream
        Inherits Stream

        Private ReadOnly inner As Stream
        Private prefix As Byte()
        Private pos As Integer = 0

        Public Sub New(inner As Stream)
            Me.inner = inner

            Dim head(2) As Byte
            Dim got = inner.Read(head, 0, 3)

            If got = 3 AndAlso head(0) = &HEF AndAlso head(1) = &HBB AndAlso head(2) = &HBF Then
                ' leading UTF-8 BOM: discard it
                prefix = New Byte(-1) {}
            Else
                prefix = New Byte(got - 1) {}
                Buffer.BlockCopy(head, 0, prefix, 0, got)
            End If
        End Sub

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return inner.CanRead
            End Get
        End Property

        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException
            End Get
        End Property

        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException
            End Get
            Set(value As Long)
                Throw New NotSupportedException
            End Set
        End Property

        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Dim total As Integer = 0

            While total < count AndAlso pos < prefix.Length
                buffer(offset + total) = prefix(pos)
                pos += 1
                total += 1
            End While

            If total < count Then
                total += inner.Read(buffer, offset + total, count - total)
            End If

            Return total
        End Function

        Public Overrides Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As Threading.CancellationToken) As Task(Of Integer)
            ' delegate to the synchronous read so behaviour matches the raw console stream
            Return Task.FromResult(Read(buffer, offset, count))
        End Function

        Public Overrides Sub Flush()
            inner.Flush()
        End Sub

        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException
        End Function

        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException
        End Sub

        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException
        End Sub
    End Class
End Class
