; Inno Setup script for SupportLauncher
; Build:   iscc /DAppVersion=1.0.0 /DSourceDir=..\publish installer\SupportLauncher.iss
;
; UNIVERSAL SETUP: the setup does not need to be rebuilt per company.
; Put these files next to SupportLauncher-Setup.exe and they are installed
; instead of the built-in defaults (all optional):
;
;   SupportLauncher-Setup.exe
;   launcher.ini     settings (download link, company name, shortcut name ...)
;   logo.png         logo shown in the launcher window
;   app.ico          icon for the shortcut and the launcher window
;
; The signed setup and EXE stay untouched, so their signatures remain valid.
;
; Silent install examples:
;   SupportLauncher-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
;   SupportLauncher-Setup.exe /VERYSILENT /URL="https://get.teamviewer.com/xyz" /COMPANY="Muster AG" /NAME="Fernwartung Muster"
;   SupportLauncher-Setup.exe /VERYSILENT /CURRENTUSER        (no admin rights needed)
; Silent uninstall:
;   "%ProgramFiles%\SupportLauncher\unins000.exe" /VERYSILENT

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

#define AppName "SupportLauncher"
#define AppExe  "SupportLauncher.exe"

[Setup]
; Keep this AppId stable across versions so updates replace the old install.
AppId={{6F2B8E61-3C1A-4E0B-9C57-2F4D8A1B7E30}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SupportLauncher contributors
AppPublisherURL=https://github.com/Kammermanns/SupportLauncher
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline dialog
OutputDir=..\dist
OutputBaseFilename=SupportLauncher-Setup
SetupIconFile=..\branding\app.ico
UninstallDisplayIcon={app}\app.ico
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
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

; Files next to the setup win. Built-in defaults are only used if no file is
; next to the setup, and never overwrite a configuration from an earlier install.
Source: "{src}\launcher.ini";          DestDir: "{app}"; Flags: external ignoreversion; Check: SrcHas('launcher.ini')
Source: "..\branding\launcher.ini";    DestDir: "{app}"; Flags: onlyifdoesntexist;     Check: not SrcHas('launcher.ini')

Source: "{src}\app.ico";               DestDir: "{app}"; Flags: external ignoreversion; Check: SrcHas('app.ico')
Source: "..\branding\app.ico";         DestDir: "{app}"; Flags: onlyifdoesntexist;     Check: not SrcHas('app.ico')

Source: "{src}\logo.png";              DestDir: "{app}"; Flags: external ignoreversion; Check: SrcHas('logo.png')
#if FileExists(AddBackslash(SourcePath) + "..\branding\logo.png")
Source: "..\branding\logo.png";        DestDir: "{app}"; Flags: onlyifdoesntexist;     Check: not SrcHas('logo.png')
#endif

[INI]
; Optional overrides from the command line (/URL=..., /COMPANY=...)
Filename: "{app}\launcher.ini"; Section: "Launcher"; Key: "DownloadUrl"; String: "{param:URL}";     Check: HasParam('URL')
Filename: "{app}\launcher.ini"; Section: "Launcher"; Key: "CompanyName"; String: "{param:COMPANY}"; Check: HasParam('COMPANY')

[Icons]
Name: "{autoprograms}\{code:GetShortcutName}"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"
Name: "{autodesktop}\{code:GetShortcutName}";  Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{code:GetShortcutName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SupportLauncher"

[Code]
const
  DefaultShortcutName = 'Fernwartung';

function HasParam(Name: String): Boolean;
begin
  Result := ExpandConstant('{param:' + Name + '|}') <> '';
end;

function SrcHas(Name: String): Boolean;
begin
  Result := FileExists(ExpandConstant('{src}\' + Name));
end;

function CleanFileName(S: String): String;
var
  I: Integer;
begin
  Result := '';
  for I := 1 to Length(S) do
    if Pos(S[I], '\/:*?"<>|') = 0 then
      Result := Result + S[I];
  Result := Trim(Result);
end;

{ Shortcut name: /NAME=... > ShortcutName in launcher.ini next to the setup
  > ShortcutName in an already installed launcher.ini > "Fernwartung". }
function GetShortcutName(Param: String): String;
var
  Ini: String;
begin
  Result := ExpandConstant('{param:NAME|}');
  if Result = '' then
  begin
    Ini := ExpandConstant('{src}\launcher.ini');
    if not FileExists(Ini) then
      Ini := ExpandConstant('{app}\launcher.ini');
    if FileExists(Ini) then
      Result := GetIniString('Launcher', 'ShortcutName', '', Ini);
  end;
  Result := CleanFileName(Result);
  if Result = '' then
    Result := DefaultShortcutName;
end;
