' 以计划任务方式启动 WinTools：任务本身带最高权限，启动时不会再弹 UAC。
' 用 WScript.Shell 的隐藏窗口参数执行，避免闪出黑色控制台窗口。
Option Explicit

Dim shell, code
Set shell = CreateObject("WScript.Shell")

code = shell.Run("schtasks /run /tn ""WinTools""", 0, True)

If code <> 0 Then
    ' 任务不存在（例如用户手动删过），回退到正常启动，让 Windows 自己弹 UAC
    shell.Run """%ProgramFiles%\WinTools\WinTools.exe""", 1, False
End If
