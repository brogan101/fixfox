#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\\dist\\FixFox_v" + AppVersion + "_win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\\dist"
#endif

#define MyAppName "FixFox"
#define MyAppPublisher "FixFox Software"
#define MyAppURL "https://github.com/brogan101/fixfox"
#define MyAppExeName "FixFox.exe"

[Setup]
AppId={{C285D616-4D80-42A1-9A8F-03E37C3F9B72}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\FixFox
DefaultGroupName=FixFox
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
SetupIconFile=..\FixFoxLogo.ico
OutputDir={#OutputDir}
OutputBaseFilename=FixFox_Setup_{#AppVersion}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\FixFox"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\FixFox"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch FixFox"; Flags: nowait postinstall skipifsilent
