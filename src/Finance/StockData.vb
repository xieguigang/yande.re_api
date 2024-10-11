Public Class StockData

    Public Property id As String
    Public Property name As String
    Public Property day As String
    Public Property time As String

    Sub New()
    End Sub

    Sub New(id As String, name As String)
        _id = id
        _name = name
    End Sub

End Class
