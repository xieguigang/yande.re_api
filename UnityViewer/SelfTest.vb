Option Strict On
Option Explicit On
Option Infer On

Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Text

''' <summary>
''' 无界面自测：对指定（或预设的）游戏文件夹执行扫描 + 抽样提取，输出报告到控制台与日志文件。
''' 用法：UnityViewer.exe --selftest [文件夹1] [文件夹2] ...
''' </summary>
Module SelfTest

    Public Sub Run(args() As String)
        Dim folders As New List(Of String)()
        If args.Length > 2 Then
            For i = 2 To args.Length - 1
                folders.Add(args(i))
            Next
        Else
            folders.Add("Z:\klsdzj_4.11.1_1_20181220_141000_676167")
            folders.Add("Z:\klsdzj_4.15.1_20190428_025416_686ef")
        End If

        Dim sb As New StringBuilder()
        sb.AppendLine("UnityViewer 自测报告  " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        For Each f In folders
            RunFolder(f, sb)
        Next

        Dim report = sb.ToString()
        Console.WriteLine(report)
        Try
            Dim logPath = Path.Combine(Path.GetTempPath(), "UnityViewer_selftest.txt")
            File.WriteAllText(logPath, report)
            Console.WriteLine("报告已写入: " & logPath)
        Catch
        End Try
    End Sub

    Private Sub RunFolder(folder As String, sb As StringBuilder)
        sb.AppendLine("========================================")
        sb.AppendLine("扫描: " & folder)
        If Not Directory.Exists(folder) Then
            sb.AppendLine("  文件夹不存在，跳过。")
            Return
        End If

        Dim sw As New Stopwatch() : sw.Start()
        Dim catalogs As List(Of AssetCatalog) = AssetScanner.ScanFolder(folder)
        sw.Stop()

        If catalogs.Count = 0 Then
            sb.AppendLine("  [诊断] 未发现来源，逐个尝试加载 .assets 以定位错误：")
            For Each f In Directory.EnumerateFiles(folder, "*.assets", SearchOption.AllDirectories)
                Try
                    Dim sf = SerializedFile.LoadFromFile(f)
                    sb.AppendLine("    OK  " & f & " 对象数=" & sf.Objects.Count)
                Catch ex As Exception
                    sb.AppendLine("    ERR " & f & " -> " & ex.GetType().Name & ": " & ex.Message)
                End Try
            Next
        End If

        Dim total As Integer = 0
        For Each c In catalogs : total += c.Count : Next
        sb.AppendLine(String.Format("  来源数: {0}, 资源数: {1}, 耗时 {2}ms", catalogs.Count, total, sw.ElapsedMilliseconds))
        For Each c In catalogs
            sb.AppendLine(String.Format("    - {0}: {1} 个资源", c.SourceLabel, c.Count))
        Next

        Dim catCount As New Dictionary(Of AssetCategory, Integer)()
        For Each c In catalogs
            For Each e In c.Entries
                If Not catCount.ContainsKey(e.Category) Then catCount.Add(e.Category, 0)
                catCount(e.Category) += 1
            Next
        Next
        sb.Append("  类别分布: ")
        For Each kv In catCount
            sb.Append(kv.Key.ToString() & "=" & kv.Value & "  ")
        Next
        sb.AppendLine()

        ' 抽样提取（每类最多 40 个）以验证解码器/布局
        Dim outDir = folder & "_extracted"
        Directory.CreateDirectory(outDir)
        Dim okCount = 0, rawCount = 0, errCount = 0
        Dim errSamples As New List(Of String)()
        Dim perCatDone As New Dictionary(Of AssetCategory, Integer)()
        For Each c In catalogs
            For Each e In c.Entries
                If Not perCatDone.ContainsKey(e.Category) Then perCatDone.Add(e.Category, 0)
                If perCatDone(e.Category) >= 40 Then Continue For
                perCatDone(e.Category) += 1
                Try
                    Dim ok = AssetExtractors.Extract(e, outDir)
                    If ok Then
                        If e.WasRawFallback Then rawCount += 1 Else okCount += 1
                    Else
                        errCount += 1
                    End If
                    If e.ErrorMessage <> "" AndAlso errSamples.Count < 25 Then
                        errSamples.Add("    " & e.SourceLabel & " / " & e.TypeName & " #" & e.PathID & ": " & e.ErrorMessage)
                    End If
                Catch ex As Exception
                    errCount += 1
                    If errSamples.Count < 25 Then
                        errSamples.Add("    " & e.SourceLabel & " / " & e.TypeName & " #" & e.PathID & ": " & ex.Message)
                    End If
                End Try
            Next
        Next
        sb.AppendLine(String.Format("  抽样提取(每类≤40): 成功可视化 {0}, 原始回退 {1}, 失败 {2}", okCount, rawCount, errCount))
        For Each s In errSamples
            sb.AppendLine(s)
        Next
    End Sub

End Module
