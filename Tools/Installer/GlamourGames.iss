; Glamour Games - Windows-Installer (Inno Setup 6)
; Aufruf: ISCC.exe /DAppVer=2.2.0 /DBuildDir=<Build-Ordner> /DOutDir=<Ausgabe> Tools\Installer\GlamourGames.iss
#ifndef AppVer
  #define AppVer "2.2.0"
#endif
#ifndef BuildDir
  #define BuildDir "..\..\Builds\Windows"
#endif
#ifndef OutDir
  #define OutDir "..\..\Builds\Installer"
#endif

[Setup]
AppId={{6C1E5B0B-4E8C-4B7E-9A4F-2D7C1F0B6A21}
AppName=Glamour Games
AppVersion={#AppVer}
AppVerName=Glamour Games {#AppVer}
AppPublisher=DoMeZos-Ware (Michael Bergfeld)
AppPublisherURL=https://github.com/domezos2024/glamour_games_unity
AppSupportURL=https://github.com/domezos2024/glamour_games_unity/issues
AppUpdatesURL=https://github.com/domezos2024/glamour_games_unity/releases
AppCopyright=Copyright (c) 2026 Michael Bergfeld @ DoMeZos-Ware
VersionInfoVersion={#AppVer}
DefaultDirName={autopf}\Glamour Games
DefaultGroupName=Glamour Games
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutDir}
OutputBaseFilename=GlamourGames-{#AppVer}-Setup
SetupIconFile=app.ico
UninstallDisplayIcon={app}\GlamourGames.exe
UninstallDisplayName=Glamour Games
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
MinVersion=10.0

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*_BurstDebugInformation_DoNotShip\*,*_BackUpThisFolder_ButDontShipItWithYourGame\*"

[Icons]
Name: "{autoprograms}\Glamour Games"; Filename: "{app}\GlamourGames.exe"
Name: "{autodesktop}\Glamour Games"; Filename: "{app}\GlamourGames.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GlamourGames.exe"; Description: "{cm:LaunchProgram,Glamour Games}"; Flags: nowait postinstall skipifsilent
