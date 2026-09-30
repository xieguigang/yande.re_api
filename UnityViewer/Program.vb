Option Strict On
Option Explicit On

Imports System.Windows.Forms

''' <summary>
''' 应用程序入口：配置高 DPI、视觉样式后启动 Form1。
''' </summary>
Module Program

    <System.STAThread()>
    Public Sub Main()
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New Form1())
    End Sub

End Module
