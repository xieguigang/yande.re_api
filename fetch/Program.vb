Imports Microsoft.VisualBasic.Serialization.JSON

Module Program

    Sub Main()
        Dim pool_id$ = App.CommandLine.Name
        Dim EXPORT$ = App.CommandLine("/export") Or $"./{pool_id}/"

        Call Moebooru.DownloadPool(pool_id, EXPORT).ToArray
        Call Moebooru.CheckPoolIntegrity(EXPORT) _
                     .GetJson _
                     .PrintException
    End Sub
End Module
