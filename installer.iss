; Compiled by build-installer.ps1, which passes /DAppVersion=x.y.z. Expects the self-contained publish output in .\publish
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{6F1A3C52-8B0E-4D6B-9A57-3C2D1E0F4A68}
AppName=Video Wallpaper
AppVersion={#AppVersion}
AppPublisher=Tsapper
DefaultDirName={autopf}\Video Wallpaper
DefaultGroupName=Video Wallpaper
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=VideoWallpaper-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\VideoWallpaper.exe
CloseApplications=force

[Tasks]
Name: "autostart"; Description: "Start with Windows"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Video Wallpaper"; Filename: "{app}\VideoWallpaper.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VideoWallpaper"; ValueData: """{app}\VideoWallpaper.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\VideoWallpaper.exe"; Description: "Launch Video Wallpaper"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM VideoWallpaper.exe"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\VideoWallpaper"
