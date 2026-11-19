#ifndef PublishDir
  #error PublishDir must be defined when compiling the installer.
#endif
#ifndef OutputDir
  #error OutputDir must be defined when compiling the installer.
#endif
#ifndef AssetDir
  #error AssetDir must be defined when compiling the installer.
#endif

#define AppName "Playlab Launcher"
#define AppVersion "1.0.0"
#define AppPublisher "Playlab"
#define AppExeName "Plataforma.Launcher.exe"

[Setup]
AppId={{F2B51C80-23F1-4B9D-A1D3-7D4A45E3F612}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Playlab\Launcher
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Plataforma-Launcher-Setup-win-x64
SetupIconFile={#AssetDir}\logo_teste.ico
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion=1.0.0.0

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Playlab Launcher"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Playlab Launcher"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Classes\playlab"; ValueType: string; ValueName: ""; ValueData: "URL:Playlab Game Launcher"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\playlab"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\playlab\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"",0"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\playlab\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Executar o Playlab Launcher"; Flags: postinstall nowait skipifsilent
