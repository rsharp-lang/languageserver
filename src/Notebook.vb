
Imports SMRUCC.Rsharp.Interpreter
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Language.TokenIcer
Imports SMRUCC.Rsharp.Runtime.Components

''' <summary>
''' A notebook data object which is parsed from the R# script.
''' </summary>
Public Class Notebook

    Public Shared Function fromRscript(rscript As Rscript) As Notebook
        Dim lines As Expression() = Expression.ParseLines(rscript, keepsCommentLines:=True).ToArray

        Return New Notebook
    End Function
End Class
