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

    Public Shared Function RequestFinance(id As IEnumerable(Of String))
        Dim url As String = $"https://hq.sinajs.cn/list={id.JoinBy(",")}"
        Dim data_str As String = url.GET(headers:=headers, refer:=Referer)

    End Function

    Public Shared Iterator Function Parse() As IEnumerable

    End Function

End Class
