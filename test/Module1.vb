Imports Microsoft.VisualBasic.Serialization.JSON

Module Module1

    Sub Main()
        'Dim p = Moebooru.Posts
        ' Dim p = Moebooru.PoolShow(5022)

        Call Moebooru.DownloadPool(5022, "tmp/5022").ToArray
        Call Moebooru.CheckPoolIntegrity("tmp/5022").GetJson.PrintException

        Pause()
    End Sub
End Module
