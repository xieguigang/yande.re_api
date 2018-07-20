Module Module1

    Sub Main()
        'Dim p = Moebooru.Posts
        ' Dim p = Moebooru.PoolShow(5022)

        Call Moebooru.DownloadPool(5022, "D:/tmp/5022")

        Pause()
    End Sub
End Module
