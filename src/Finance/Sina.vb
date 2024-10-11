Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.Closure
Imports SMRUCC.Rsharp.Interpreter.ExecuteEngine.ExpressionSymbols.DataSets
Imports SMRUCC.Rsharp.Runtime.Components

''' <summary>
''' 
''' </summary>
''' <remarks>
''' https://finance.sina.com.cn
''' </remarks>
Public Class Sina

    Public Const Referer As String = "https://finance.sina.com.cn"

    Shared ReadOnly headers As New Dictionary(Of String, String) From {
        {"Referer", Referer}
    }

    Public Shared Function RequestFinance(id As IEnumerable(Of String)) As StockData()
        Dim url As String = $"https://hq.sinajs.cn/list={id.JoinBy(",")}"
        Dim data_str As String = url.GET(headers:=headers, refer:=Referer)

        Return Parse(data_str).ToArray
    End Function

    ''' <summary>
    ''' parse of the sina finance data
    ''' </summary>
    ''' <param name="data_str"></param>
    ''' <returns></returns>
    Public Shared Iterator Function Parse(data_str As String) As IEnumerable(Of StockData)
        Dim lines As Expression() = Expression.ParseLines(SMRUCC.Rsharp.Runtime.Components.Rscript.FromText(data_str)).ToArray

        For Each one As Expression In lines
            Dim var As DeclareNewSymbol = one
            Dim name = var.GetSymbolName.Replace("hq_str_", "")
            Dim vals = DirectCast(var.value, Literal).ValueStr.Split(","c)

            Yield New StockData(name, vals(0))
        Next
    End Function

End Class

