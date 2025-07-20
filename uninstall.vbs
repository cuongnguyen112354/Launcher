Set WshShell = CreateObject("WScript.Shell")
desktopPath = WshShell.SpecialFolders("Desktop")
Set shortcut = WshShell.CreateShortcut(desktopPath & "\Uninstaller.lnk")

shortcut.TargetPath = "C:\Pulse Frenzy Launcher\Launcher\uninstall.bat"
shortcut.WorkingDirectory = "C:\Pulse Frenzy Launcher\Launcher"
shortcut.IconLocation = "C:\Pulse Frenzy Launcher\Launcher\Resources\Launch.ico"

shortcut.Save