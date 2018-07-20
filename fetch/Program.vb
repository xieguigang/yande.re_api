Imports Microsoft.VisualBasic.Serialization.JSON

Module Program

    Sub Main()
        Dim pool_id$ = App.CommandLine.Name
        Dim EXPORT$ = App.CommandLine("/export") Or $"./{pool_id}/"

        If pool_id.StringEmpty Then
            Call Console.WriteLine("Usage: fetch <pool_id> [/export <directory>]")
            Return
        End If

        Call Moebooru.DownloadPool(pool_id, EXPORT).ToArray
        Call Moebooru.CheckPoolIntegrity(EXPORT) _
                     .GetJson _
                     .PrintException
    End Sub
End Module
