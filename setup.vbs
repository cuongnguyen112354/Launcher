Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

' Đường dẫn đến script hiện tại
currentScript = WScript.ScriptFullName
currentFolder = fso.GetParentFolderName(currentScript)

' Chạy file uninstall.vbs nếu cần
shell.Run "wscript.exe """ & currentFolder & "\uninstall.vbs""", 0, False

' Tạo shortcut cho Launcher.exe
iconPath = currentFolder & "\Resources\Launch.ico"
exePath = currentFolder & "\bin\Debug\Launcher.exe"
desktopPath = shell.SpecialFolders("Desktop")

Set shortcut = shell.CreateShortcut(desktopPath & "\Launcher.lnk")
shortcut.TargetPath = exePath
shortcut.WorkingDirectory = currentFolder
shortcut.IconLocation = iconPath
shortcut.WindowStyle = 1
shortcut.Description = "Khởi chạy Launcher"
shortcut.Save
