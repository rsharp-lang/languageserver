Imports System.Runtime.CompilerServices
Imports System.Text
Imports Microsoft.VisualBasic.ApplicationServices.Development.XmlDoc.Assembly
Imports Microsoft.VisualBasic.Linq
Imports Microsoft.VisualBasic.Serialization
Imports Microsoft.VisualBasic.Text.Xml.Models
Imports SMRUCC.Rsharp.Development
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Runtime
Imports SMRUCC.Rsharp.Runtime.Internal.ConsolePrinter
Imports SMRUCC.Rsharp.Runtime.Interop

Namespace LspMarkdown

    Module Method

        Public Function BuildHoverMarkdown(value As Object, name As String, env As Environment) As String
            Dim sb As New StringBuilder

            If TypeOf value Is RMethodInfo Then
                Dim f As RMethodInfo = DirectCast(value, RMethodInfo)
                Dim doc As ProjectMember = env.globalEnvironment _
                    .packages _
                    .packageDocs.GetAnnotations(f.GetNetCoreCLRDeclaration)

                Call sb.AppendLine(f.html(If(doc, New ProjectMember)))
            ElseIf value.GetType.IsInheritsFrom(GetType(SymbolExpression)) Then
                Call sb.AppendLine(DirectCast(value, SymbolExpression).html)
            Else
                Return Nothing
            End If

            Return sb.ToString
        End Function

        <Extension>
        Public Function html(fun As SymbolExpression) As String
            Dim help As Document = fun.TryGetHelpDocument
            Dim sb As New StringBuilder

            Call sb.AppendLine($"# {fun.GetSymbolName} {{package: {fun.expressionName.Description}}}")

            If Not help Is Nothing Then
                Call sb.AppendLine(help.title)
                Call sb.AppendLine()
                Call sb.AppendLine("## description")
                Call sb.AppendLine(help.description)
                Call sb.AppendLine("## details")
                Call sb.AppendLine(help.details)
                Call sb.AppendLine("## arguments")

                For Each arg As NamedValue In help.parameters.SafeQuery
                    Call sb.AppendLine("#### " & arg.name)
                    Call sb.AppendLine(arg.text)
                Next

                Call sb.AppendLine("## returns")
                Call sb.AppendLine(help.returns)
                Call sb.AppendLine("## examples")
                Call sb.AppendLine(help.examples)
            End If

            Call sb.AppendLine(fun.ToString)

            Return sb.ToString
        End Function

        <Extension>
        Public Function html(api As RMethodInfo, doc As ProjectMember) As String
            Dim raw As Type = api.GetNetCoreCLRDeclaration().DeclaringType
            Dim rawDeclare$ = raw.FullName
            Dim packageName$ = raw.NamespaceEntry(True).Namespace
            Dim params$
            Dim returns As IRType() = RApiReturnAttribute _
                .GetActualReturnType(api.GetNetCoreCLRDeclaration) _
                .ToArray

            If api.parameters.Length > 3 Then
                params = api.parameters.Select(Function(a) a.html).JoinBy(", " & vbCrLf & vbCrLf & "&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;")
            Else
                params = api.parameters.Select(Function(a) a.html).JoinBy(", ")
            End If

            Dim head As String = doc.Summary
            Dim desc As String = doc.Remarks
            Dim vals As String = doc.Returns
            Dim args As New List(Of String)

            If Not doc.Params.IsNullOrEmpty Then
                For Each a In doc.Params
                    Call args.Add("#### " & a.name)
                    Call args.Add(a.text)
                    Call args.Add(vbCrLf)
                Next
            End If

            Return $"
{head}

## description

{desc}

## arguments

{args.JoinBy(vbCrLf)}

## returns

{vals}

## declares

let <code>{api.name}</code> as function({params}) -> ``{returns.JoinBy("|")}`` {{{vbCrLf & vbCrLf}
 ```
    # 
    # .NET clr API information 
    # 
    # module: {rawDeclare} 
    # LibPath: {raw.Assembly.Location.ParentPath} 
    # library: {raw.Assembly.Location.FileName} 
    # package: ""{packageName}"" 
    # stackframe: {api.stackFrame}
    #
    return call R-sharp.interop_[{api.ToString}](...);
```
}}
    
## example
    
```r
{doc.example}
```
    "
        End Function

        <Extension>
        Public Function html(arg As RMethodArgument) As String
            Dim defaultValue As String = "<code><NULL></code>"
            Dim arg_default As Object = arg.default

            If arg_default Is Nothing Then
                defaultValue = "<code><NULL></code>"
            ElseIf arg.isOptional Then
                If arg_default.GetType Is GetType(String) Then
                    defaultValue = $"<span style='color:brown'>""{arg_default}""</span>"
                ElseIf arg.type.raw.IsEnum Then
                    defaultValue = enumPrinter.defaultValueToString(arg_default, arg.type)
                ElseIf Not arg.defaultScriptValue Is Nothing Then
                    defaultValue = $"'{arg.defaultScriptValue.defaultValue}'"
                ElseIf arg_default.GetType.IsArray Then
                    defaultValue = Json.GetObjectJson(arg_default.GetType, arg_default, indent:=False)
                Else
                    defaultValue = arg_default.ToString.ToUpper
                End If
            End If

            If arg.isObjectList Then
                Return "..."
            End If

            If arg.type.isEnvironment Then
                Return $"[<code><Environment></code>]"
            End If

            If arg.isOptional Then
                Return $"<code>{arg.name}<code> as {arg.type} = {defaultValue}"
            Else
                Return $"<code>{arg.name}<code> as {arg.type}"
            End If
        End Function
    End Module
End Namespace