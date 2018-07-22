Imports Microsoft.VisualBasic.Serialization.JSON

Module Program

    ReadOnly pendingTask$ = App.LocalData & "/pending.json"

    Sub Main()
        Dim pool_id$ = App.CommandLine.Name
        Dim EXPORT$ = App.CommandLine("/export") Or $"./{pool_id}/"

        Call pendingTask.__DEBUG_ECHO

        If pool_id.StringEmpty Then
            Call Console.WriteLine("Usage: fetch <pool_id> [/export <directory>]")
            Return
        ElseIf pool_id.TextEquals("pending") Then
            pool_id = App.CommandLine.Parameters(0)
            save(pool_id, 0)

            Call pendingTask.LoadObject(Of Dictionary(Of String, String)) _
                            .Where(Function(task) task.Value = "0") _
                            .ToDictionary _
                            .GetJson(indent:=True) _
                            .__INFO_ECHO
            Return
        End If
re0:
        Call $" => {pool_id}".Warning
        Call Moebooru.DownloadPool(pool_id, EXPORT).ToArray
        Call Moebooru.CheckPoolIntegrity(EXPORT) _
                     .GetJson _
                     .PrintException

        Call save(pool_id, 1)

        If pendingTask.FileExists Then
            pool_id = pendingTask.LoadObject(Of Dictionary(Of String, String)) _
                                 .Where(Function(task) task.Value = "0") _
                                 .FirstOrDefault _
                                 .Key
            EXPORT = $"./{pool_id}/"

            If Not pool_id.StringEmpty Then
                GoTo re0
            End If
        End If
    End Sub

    Private Sub save(pool_id$, status$)
        Dim data As Dictionary(Of String, String)

        If Not pendingTask.FileExists Then
            data = New Dictionary(Of String, String)
        Else
            data = pendingTask.LoadObject(Of Dictionary(Of String, String))
        End If

        data(pool_id) = status
        data.GetJson.SaveTo(pendingTask)
    End Sub
End Module
