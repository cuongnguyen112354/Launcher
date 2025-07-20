@echo off
echo Đang gỡ cài đặt Pulse Frenzy Launcher...

:: Xóa shortcut trên Desktop
del "%USERPROFILE%\Desktop\Launcher.lnk" >nul 2>&1
del "%USERPROFILE%\Desktop\Uninstaller.lnk" >nul 2>&1

:: Xóa thư mục cài đặt
rmdir /s /q "C:\Pulse Frenzy Launcher"

echo Gỡ cài đặt hoàn tất.
pause
