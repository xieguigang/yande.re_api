Option Strict On
Option Explicit On

Imports System.IO

''' <summary>
''' 扫描安卓游戏解包后的文件夹，定位并解析所有 Unity 资源容器：
''' - bin/Data/*.assets（SerializedFile）
''' - assets/Android/* 无扩展名文件（UnityFS 资源包）
''' 解析后收集可提取资源到 AssetCatalog。
''' </summary>
Public Module AssetScanner

    ''' <summary>扫描文件夹，返回按来源分组的资源目录。</summary>
    Public Function ScanFolder(root As String) As List(Of AssetCatalog)
        Dim catalogs As New List(Of AssetCatalog)()

        If Not Directory.Exists(root) Then Return catalogs

        ' 1) 所有 .assets 文件
        For Each f In Directory.EnumerateFiles(root, "*.assets", SearchOption.AllDirectories)
            Try
                Dim sf = SerializedFile.LoadFromFile(f)
                Dim cat = BuildCatalog(sf, f)
                If cat.Entries.Count > 0 Then catalogs.Add(cat)
            Catch ex As Exception
                ' 某些 .assets 可能不是 SerializedFile，忽略
            End Try
        Next

        ' 2) assets/Android 下的无扩展名文件（UnityFS 资源包）
        Dim androidDir = Path.Combine(root, "assets", "Android")
        If Directory.Exists(androidDir) Then
            For Each f In Directory.EnumerateFiles(androidDir)
                ' 跳过明显非包的文件（如 .csv/.txt/.xml 等）
                Dim ext = Path.GetExtension(f).ToLower()
                If ext = ".csv" OrElse ext = ".txt" OrElse ext = ".xml" OrElse ext = ".json" OrElse
                   ext = ".db" OrElse ext = ".json" Then Continue For
                Try
                    Dim bytes = File.ReadAllBytes(f)
                    If bytes.Length < 20 Then Continue For
                    If System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(7, bytes.Length)) <> "UnityFS" Then Continue For
                    Dim bundle = UnityFSBundle.Load(bytes)
                    Dim cat = BuildCatalogFromBundle(bundle, f)
                    If cat.Entries.Count > 0 Then catalogs.Add(cat)
                Catch ex As Exception
                    ' 非 UnityFS 包，忽略
                End Try
            Next
        End If

        Return catalogs
    End Function

    Private Function BuildCatalog(sf As SerializedFile, sourcePath As String) As AssetCatalog
        Dim cat As New AssetCatalog()
        cat.SourceLabel = Path.GetFileName(sourcePath)
        For Each obj In sf.Objects
            If Not IsExtractableType(obj.ClassID) Then Continue For
            Dim entry As New AssetEntry()
            entry.File = sf
            entry.Obj = obj
            entry.ClassID = obj.ClassID
            entry.TypeName = ClassIDToName(obj.ClassID)
            entry.PathID = obj.PathID
            entry.Category = CategoryOf(obj.ClassID)
            entry.SourceLabel = cat.SourceLabel
            entry.Size = obj.Length
            cat.Entries.Add(entry)
        Next
        Return cat
    End Function

    Private Function BuildCatalogFromBundle(bundle As UnityFSBundle, sourcePath As String) As AssetCatalog
        Dim cat As New AssetCatalog()
        cat.SourceLabel = Path.GetFileName(sourcePath)
        For Each e In bundle.Entries
            If e.Data Is Nothing OrElse e.Data.Length < 20 Then Continue For
            Try
                Dim sf = SerializedFile.LoadFromBundleEntry(e, bundle, e.Name)
                ' 仅当能解析出对象表时才视为 SerializedFile
                If sf.Objects.Count = 0 AndAlso e.Name.StartsWith("CAB-", StringComparison.OrdinalIgnoreCase) = False Then
                    ' 非 CAB 且无对象，跳过
                End If
                For Each obj In sf.Objects
                    If Not IsExtractableType(obj.ClassID) Then Continue For
                    Dim entry As New AssetEntry()
                    entry.File = sf
                    entry.Obj = obj
                    entry.ClassID = obj.ClassID
                    entry.TypeName = ClassIDToName(obj.ClassID)
                    entry.PathID = obj.PathID
                    entry.Category = CategoryOf(obj.ClassID)
                    entry.SourceLabel = cat.SourceLabel & " / " & e.Name
                    entry.Size = obj.Length
                    cat.Entries.Add(entry)
                Next
            Catch
                ' 该 entry 不是 SerializedFile，忽略
            End Try
        Next
        Return cat
    End Function

End Module
