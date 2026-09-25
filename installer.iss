#define AppName "Wavelog 管家"
#define AppVersion "0.3.0"
#define AppPublisher "miaodegu7"
#define AppExeName "WavelogButler.exe"
[Setup]
AppId={{9B5D4F7B-3E1B-4F9B-9D40-2E5D8E1F0B31}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\WavelogButler
DefaultGroupName={#AppName}
OutputDir=dist\installer
OutputBaseFilename=WavelogButler-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
SetupIconFile=Assets\wavelog-butler.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
[Files]
Source: "dist\standalone\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 {#AppName}"; Flags: nowait postinstall skipifsilent
