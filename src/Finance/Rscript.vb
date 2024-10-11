
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Scripting.MetaData
Imports SMRUCC.Rsharp.Runtime.Interop
Imports SMRUCC.Rsharp.Runtime.Vectorization

<Package("analyst")>
Module Rscript

    <ExportAPI("get_sina_stock")>
    Public Function get_sina_stock(<RRawVectorArgument> id As Object) As StockData()
        Return Sina.RequestFinance(CLRVector.asCharacter(id))
    End Function

End Module
