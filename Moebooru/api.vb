Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.VisualBasic.ApplicationServices
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Terminal.ProgressBar
Imports Moebooru.Models

<HideModuleName> Public Module api

    Const base$ = "https://yande.re"

    ReadOnly apis As Dictionary(Of String, String)

    Private Function getURL(<CallerMemberName> Optional key$ = Nothing) As String
        Return $"{api.base}/{apis(key)}"
    End Function

    Sub New()
        apis = GetType(api) _
            .GetMethods(BindingFlags.Public Or BindingFlags.Static) _
            .Where(Function(m)
                       Return Not m.GetCustomAttribute(Of ExportAPIAttribute) Is Nothing
                   End Function) _
            .ToDictionary(Function(m) m.Name,
                          Function(m)
                              Return m.GetCustomAttribute(Of ExportAPIAttribute).Name
                          End Function)
    End Sub

    ''' <summary>
    ''' 
    ''' </summary>
    ''' <param name="limit%">How many posts you want to retrieve. There is a hard limit of 100 posts per request.</param>
    ''' <param name="page%">The page number.</param>
    ''' <param name="tags">
    ''' The tags to search for. Any tag combination that works on the web site will work here. This includes all the meta-tags.
    ''' </param>
    ''' <returns></returns>
    <ExportAPI("post.xml")>
    Public Function Posts(Optional limit% = -1, Optional page% = -1, Optional tags As IEnumerable(Of String) = Nothing) As Posts
        Dim url$ = getURL()
        Dim out = url.GET.LoadFromXml(Of Posts)
        Return out
    End Function

    <ExportAPI("pool/show.xml")>
    Public Function PoolShow(id As String) As Pool
        Dim url$ = getURL() & $"?id={id}"
        Dim out = url.GET.LoadFromXml(Of Pool)
        Return out
    End Function

    Public Iterator Function DownloadPool(id$, EXPORT$) As IEnumerable(Of (file$, success As Boolean))
        Dim pool As Pool = api.PoolShow(id)

        Using progressBar As New ProgressBar("Download pool images...")
            Dim task As New ProgressProvider(total:=pool.posts.Length)
            Dim msg$

            For Each post As post In pool.posts
                Dim url$ = post.file_url
                Dim save$ = $"{EXPORT}/{post.id}.{url.ExtensionSuffix}"

                msg = $" ==> {url} [ETA={task.ETA(progressBar.ElapsedMilliseconds).FormatTime}]"

                Call Thread.Sleep(10 * 1000)
                Call progressBar.SetProgress(task.StepProgress, msg)

                Yield (url, url.DownloadFile(save,))
            Next
        End Using

        Call pool.GetXml.SaveTo($"{EXPORT}/index.xml")
        Call GZip.DirectoryArchive(EXPORT, $"{EXPORT.ParentPath}/{pool.name.NormalizePathString(False)}.zip")
    End Function

    ''' <summary>
    ''' 返回缺失的post编号
    ''' </summary>
    ''' <param name="directory"></param>
    ''' <returns></returns>
    Public Function CheckPoolIntegrity(directory As String) As Integer()
        Dim index As Pool = $"{directory}/index.xml".LoadXml(Of Pool)
        Dim missing As New List(Of Integer)

        For Each post In index.posts
            Dim file$ = $"{directory}/{post.id}.{post.file_url.ExtensionSuffix}"
            Dim test = file.FileLength > 0

            If Not test = True Then
                missing += post.id
            End If
        Next

        Return missing
    End Function
End Module
