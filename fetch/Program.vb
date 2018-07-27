Imports System.IO.Compression
Imports System.Threading
Imports Microsoft.VisualBasic.ApplicationServices
Imports Microsoft.VisualBasic.Serialization.JSON
Imports Moebooru.Models

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
        ElseIf pool_id.TextEquals("/scan.missing") Then
            Dim finished = pendingTask _
                .LoadObject(Of Dictionary(Of String, String)) _
                .Where(Function(task) task.Value <> "0") _
                .ToArray

            For Each id As String In finished.Keys
                Dim index$ = $"./{id}/index.xml"

                If Not index.FileExists Then
                    pool_id = id
                    EXPORT = index.ParentPath

                    Call Moebooru _
                        .DownloadPool(pool_id, EXPORT) _
                        .ToArray
                Else
                    Dim pool As Pool = index.LoadXml(Of Pool)
                    Dim directory = index.ParentPath
                    Dim zip$ = $"{directory.ParentPath}/{pool.name.NormalizePathString(False)}.zip"

                    For Each post In pool.posts
                        Dim file$ = $"{directory}/{post.id}.{post.file_url.ExtensionSuffix}"
                        Dim test = file.FileLength > 0

                        If Not test = True Then
                            Call post.file_url.DownloadFile(file,)
                            Call Thread.Sleep(10 * 1000)
                        End If
                    Next

                    Call GZip.DirectoryArchive(
                        directory, zip,
                        ArchiveAction.Replace,
                        Overwrite.Always,
                        CompressionLevel.Fastest,
                        True
                    )
                    Call zip.__INFO_ECHO
                End If
            Next

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
