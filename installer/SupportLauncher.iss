; Inno Setup script for SupportLauncher
; Build:   iscc /DAppVersion=1.0.0 /DSourceDir=..\publish installer\SupportLauncher.iss
;
; Silent install examples:
;   SupportLauncher-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
;   SupportLauncher-Setup.exe /VERYSILENT /URL="https://get.teamviewer.com/xyz" /COMPANY="Muster AG"
;   SupportLauncher-Setup.exe /VERYSILENT /CURRENTUSER        (no admin rights needed)
; Silent uninstall:
;   "%ProgramFiles%\SupportLauncher\unins000.exe" /VERYSILENT

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif
#ifndef ShortcutName
  #define ShortcutName "Fernwartung"
#endif

#define AppName "SupportLauncher"
#define AppExe  "SupportLauncher.exe"

[Setup]
; Keep this AppId stable across versions so updates replace the old install.
AppId={{6F2B8E61-3C1A-4E0B-9C57-2F4D8A1B7E30}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SupportLauncher contributors
AppPublisherURL=https://github.com/Kammermanns/SupportLuncher
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#ShortcutName}
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline dialog
OutputDir=..\dist
OutputBaseFilename=SupportLauncher-Setup
SetupIconFile=..\branding\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
VersionInfoVersion={#AppVersion}

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\{#AppExe}";   DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\launcher.ini"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\logo.png";     DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[INI]
; Optional overrides from the command line (/URL=..., /COMPANY=...)
Filename: "{app}\launcher.ini"; Section: "Launcher"; Key: "DownloadUrl"; String: "{param:URL}";     Check: HasParam('URL')
Filename: "{app}\launcher.ini"; Section: "Launcher"; Key: "CompanyName"; String: "{param:COMPANY}"; Check: HasParam('COMPANY')

[Icons]
Name: "{group}\{#ShortcutName}";       Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#ShortcutName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#ShortcutName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SupportLauncher"

[Code]
function HasParam(Name: String): Boolean;
begin
  Result := ExpandConstant('{param:' + Name + '|}') <> '';
end;
