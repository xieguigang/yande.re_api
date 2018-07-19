Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.CommandLine.Reflection
Imports Moebooru.Models

<HideModuleName> Public Module api

    Const base$ = "https://yande.re"

    ReadOnly apis As Dictionary(Of String, String)

    Private Function getURL(<CallerMemberName> Optional key$ = Nothing) As String
        Return $"{api.base}/{apis(key)}"
    End Function

    Sub New()
        apis = GetType(api) _
            .GetMethods(BindingFlags.Public) _
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
        Dim out = url.GET.LoadXml(Of Posts)
        Return out
    End Function
End Module
