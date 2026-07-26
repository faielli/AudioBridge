; AudioBridge — Windows installer (Inno Setup)
; Build desktop first:
;   dotnet publish desktop/AudioBridge.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/windows

#define MyAppName "AudioBridge"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "AudioBridge"
#define MyAppURL "https://github.com/federico/AudioBridge"
#define MyAppExeName "AudioBridge.Desktop.exe"

[Setup]
AppId={{B8F4C3A2-1D5E-4F7A-9B6C-3E2D1A0F8C7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir=installer
OutputBaseFilename=AudioBridge-Setup-{#MyAppVersion}
SetupIconFile=desktop\AudioBridge.Desktop\Assets\audiobridge.ico
UninstallDisplayName={#MyAppName} {#MyAppVersion}
PrivilegesRequired=admin

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce
Name: "autostart"; Description: "Launch automatically on &login"; GroupDescription: "Startup options:"; Flags: unchecked

[UninstallRun]
Filename: "taskkill"; Parameters: "/F /IM {#MyAppExeName}"; \
    Flags: runhidden waituntilterminated; RunOnceId: "KillAudioBridge"
    
[Files]
Source: "publish\windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Autostart via HKCU (no admin required for run key)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent

