Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Microsoft.VisualBasic.Imaging
Imports Microsoft.VisualBasic.Linq
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

        With tags.SafeQuery.ToArray
            If .Length > 0 Then
                url = $"{url}?tags={ .JoinBy("+")}"
            End If
        End With

        If page > 0 Then
            url = $"{url}&page={page}"
        End If

        Dim out = url.GET.LoadFromXml(Of Posts)
        Return out
    End Function

    <ExportAPI("pool/show.xml")>
    Public Function PoolShow(id As String) As Pool
        Dim url$ = getURL() & $"?id={id}"
        Dim out = url.GET.LoadFromXml(Of Pool)
        Return out
    End Function

    ''' <summary>
    ''' 这个函数会自动跳过已经存在的文件的下载操作
    ''' </summary>
    ''' <param name="id$"></param>
    ''' <param name="EXPORT$"></param>
    ''' <returns></returns>
    Public Iterator Function DownloadPool(id$, EXPORT$) As IEnumerable(Of (file$, success As Boolean))
        Dim pool As Pool = api.PoolShow(id)

        Using progressBar As New ProgressBar("Download pool images...")
            Dim task As New ProgressProvider(total:=pool.posts.Length)
            Dim msg$

            For Each post As post In pool.posts
                Dim url$ = post.file_url
                Dim save$ = $"{EXPORT}/{post.id}.{url.ExtensionSuffix}"

                msg = $" ==> {url.BaseName(allowEmpty:=True)} [ETA={task.ETA(progressBar.ElapsedMilliseconds).FormatTime}]"

                Call progressBar.SetProgress(task.StepProgress, msg)

                If url.StringEmpty Then
                    Continue For
                End If

                If Not save.FileExists OrElse save.LoadImage(throwEx:=False) Is Nothing Then
                    Yield (url, url.DownloadFile(save,))
                    Call Thread.Sleep(10 * 1000)
                Else
                    Yield (url, True)
                End If
            Next
        End Using

        Call pool.GetXml.SaveTo($"{EXPORT}/index.xml")
    End Function
End Module
