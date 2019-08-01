Imports System.IO.Compression
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.VisualBasic.ApplicationServices
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.CommandLine
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Serialization.JSON
Imports Moebooru
Imports Moebooru.Models

Module Program

    ReadOnly pendingTask$ = App.LocalData & "/pending.json"

    Public Function Main() As Integer
        Return GetType(Program).RunCLI(App.CommandLine,
            executeEmpty:=AddressOf taskResume,
            executeNotFound:=AddressOf fetchByPoolId
        )
    End Function

    <ExportAPI("/posts")>
    <Usage("/posts /tags <tagsList, a+b+c> [/out <directory>]")>
    Public Function DownloadPosts(args As CommandLine) As Integer
        Dim tags$ = args <= "/tags"
        Dim out$ = args("/out") Or $"./{tags.NormalizePathString(False)}/"

        Call Task.DownloadPost(tags.Split("+"c), EXPORT:=out)

        Return 0
    End Function

    Private Function fetchByPoolId(args As CommandLine) As Integer
        Dim pool_id$ = args.Name
        Dim EXPORT$ = args("/export") Or $"./{pool_id}/"

        Call pendingTask.__DEBUG_ECHO
        Call pool_id.taskWorker(EXPORT)

        Return 0
    End Function

    <Extension> Private Sub taskWorker(pool_id$, EXPORT$)
re0:
        Call $" => {pool_id}".__DEBUG_ECHO
        Call Moebooru.Task.DownloadPool(pool_id, EXPORT).ToArray
        Call Moebooru.CheckPoolIntegrity(EXPORT) _
                     .GetJson _
                     .PrintException

        Call save(pool_id, 1)

        If pendingTask.FileExists Then
            pool_id = pendingTask.LoadJSON(Of Dictionary(Of String, String)) _
                                 .Where(Function(task) task.Value = "0") _
                                 .FirstOrDefault _
                                 .Key
            EXPORT = $"./{pool_id}/"

            If Not pool_id.StringEmpty Then
                GoTo re0
            End If
        End If
    End Sub

    ''' <summary>
    ''' Execute when cli is empty
    ''' </summary>
    ''' <returns></returns>
    Private Function taskResume() As Integer
        Dim pool_id$ = pendingTask.LoadJSON(Of Dictionary(Of String, String)) _
                                  .Where(Function(task) task.Value = "0") _
                                  .FirstOrDefault _
                                  .Key

        If Not pool_id.StringEmpty Then
            Call pool_id.taskWorker(EXPORT:=$"./{pool_id}/")
        Else
            Call Console.WriteLine("Usage: fetch <pool_id> [/export <directory>]")
        End If

        Return 0
    End Function

    <ExportAPI("/scan.missing")>
    Public Function ScanMissing(args As CommandLine) As Integer
        Dim finished = pendingTask _
            .LoadJSON(Of Dictionary(Of String, String)) _
            .Where(Function(task) task.Value <> "0") _
            .ToArray

        For Each id As String In finished.Keys
            Call id.checkInternal
        Next

        Return 0
    End Function

    <Extension> Private Sub checkInternal(id As String)
        Dim pool_id$, EXPORT$
        Dim index$ = $"./{id}/index.xml"

        If Not index.FileExists Then
            pool_id = id
            EXPORT = index.ParentPath

            Call Moebooru.Task _
                .DownloadPool(pool_id, EXPORT) _
                .ToArray
        Else
            Dim pool As Pool = index.LoadXml(Of Pool)
            Dim directory = index.ParentPath
            Dim zip$ = $"{directory.ParentPath}/{pool.PoolZipName}"

            For Each post In pool.posts
                Dim file$ = $"{directory}/{post.id}.{post.file_url.ExtensionSuffix}"
                Dim test = file.FileLength > 0

                If Not test = True Then
                    Call post.file_url.DownloadFile(file,)
                    Call Thread.Sleep(10 * 1000)
                End If
            Next

            Call ZipLib.DirectoryArchive(
                directory, zip,
                ArchiveAction.Replace,
                Overwrite.Always,
                CompressionLevel.Fastest,
                True
            )
            Call zip.__INFO_ECHO
        End If
    End Sub

    <ExportAPI("/pending")>
    Public Function PendingTaskCLI(args As CommandLine) As Integer
        Dim pool_id = App.CommandLine.Parameters(0)

        Call Program.save(pool_id, 0)
        Call pendingTask.LoadJSON(Of Dictionary(Of String, String)) _
                        .Where(Function(task) task.Value = "0") _
                        .Select(Function(t) {t.Key, t.Value}) _
                        .AppendAfter({New String() {"pool_id", "status"}}) _
                        .ToArray _
                        .Print(dev:=App.StdOut)
        Return 0
    End Function

    Private Sub save(pool_id$, status$)
        Dim data As Dictionary(Of String, String)

        If Not pendingTask.FileExists Then
            data = New Dictionary(Of String, String)
        Else
            data = pendingTask.LoadJSON(Of Dictionary(Of String, String))
        End If

        data(pool_id) = status
        data.GetJson.SaveTo(pendingTask)
    End Sub
End Module
