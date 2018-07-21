Imports Microsoft.VisualBasic.Serialization.JSON

Module Program

    ReadOnly pendingTask$ = App.LocalData & "/pending.json"

    Sub Main()
        Dim pool_id$ = App.CommandLine.Name
        Dim EXPORT$ = App.CommandLine("/export") Or $"./{pool_id}/"

        If pool_id.StringEmpty Then
            Call Console.WriteLine("Usage: fetch <pool_id> [/export <directory>]")
            Return
        ElseIf pool_id.TextEquals("pending") Then
            pool_id = App.CommandLine.Parameters(0)

            If Not pendingTask.FileExists Then
                Call New Dictionary(Of String, String) From {
                    {pool_id, 0}
                }.GetJson _
                 .SaveTo(pendingTask)
            Else
                With pendingTask.LoadObject(Of Dictionary(Of String, String))
                    .Item(pool_id) = 0
                    .GetJson _
                    .SaveTo(pendingTask)
                End With
            End If

            Call pendingTask.LoadObject(Of Dictionary(Of String, String)) _
                            .Where(Function(task) task.Value = "0") _
                            .ToDictionary _
                            .GetJson(indent:=True) _
                            .__INFO_ECHO
            Return
        End If
re0:
        Call Moebooru.DownloadPool(pool_id, EXPORT).ToArray
        Call Moebooru.CheckPoolIntegrity(EXPORT) _
                     .GetJson _
                     .PrintException

        If pendingTask.FileExists Then
            pool_id = pendingTask.LoadObject(Of Dictionary(Of String, String)) _
                                 .Where(Function(task) task.Value = "0") _
                                 .FirstOrDefault _
                                 .Key

            If Not pool_id.StringEmpty Then
                GoTo re0
            End If
        End If
    End Sub
End Module
