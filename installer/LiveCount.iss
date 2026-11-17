; Installer di LiveCount (Inno Setup 6).
; Si compila con installer\build-installer.ps1, che prima compila LiveCount.exe con build.bat
; e poi passa a questo script la versione letta dall'exe.
; L'exe resta anche "portabile": l'installer serve a chi vuole menu Start, collegamento sul
; desktop e disinstallazione da "App installate" come per gli altri programmi.

#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#ifndef ExePath
  #define ExePath "..\LiveCount.exe"
#endif

[Setup]
; AppId fisso per sempre: e' cio' che fa riconoscere e aggiornare un'installazione esistente.
AppId={{766B9C0E-13AB-4EC5-81CF-A64E3E3C3441}
AppName=LiveCount
AppVersion={#AppVersion}
AppVerName=LiveCount {#AppVersion}
AppPublisher=LiveCount
AppPublisherURL=https://github.com/ncrdavide-svg/LiveCount
AppSupportURL=https://github.com/ncrdavide-svg/LiveCount
AppUpdatesURL=https://github.com/ncrdavide-svg/LiveCount/releases
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Installazione di LiveCount
DefaultDirName={autopf}\LiveCount
DefaultGroupName=LiveCount
DisableProgramGroupPage=yes
SetupIconFile=..\assets\LiveCount.ico
UninstallDisplayIcon={app}\LiveCount.exe
UninstallDisplayName=LiveCount
OutputDir=..\dist
OutputBaseFilename=LiveCount-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Per tutti gli utenti (chiede i permessi di amministratore) oppure "solo per me" (senza permessi).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Windows 10/11 a 64 bit (l'exe e' x64).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Se LiveCount e' aperto durante un aggiornamento o la disinstallazione, propone di chiuderlo.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#ExePath}"; DestDir: "{app}"; DestName: "LiveCount.exe"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\LiveCount"; Filename: "{app}\LiveCount.exe"; Comment: "Timer countdown per eventi dal vivo"
Name: "{autodesktop}\LiveCount"; Filename: "{app}\LiveCount.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LiveCount.exe"; Description: "{cm:LaunchProgram,LiveCount}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Libreria NDI estratta dall'exe al primo avvio (%LOCALAPPDATA%\LiveCount\ndi-<hash>): e' solo una cache.
Type: filesandordirs; Name: "{localappdata}\LiveCount"
Type: files; Name: "{app}\LiveCount.settings.xml.tmp"

[Code]
// Preset e impostazioni: accanto all'exe se la cartella e' scrivibile (installazione "solo per me"),
// altrimenti in %APPDATA%\LiveCount (installazione per tutti in Programmi). Vedi Settings.FilePath.
function SettingsExist: Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\LiveCount.settings.xml')) or
            DirExists(ExpandConstant('{userappdata}\LiveCount'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and SettingsExist then
  begin
    if SuppressibleMsgBox('Vuoi eliminare anche i preset e le impostazioni di LiveCount?' + #13#10#13#10 +
         'Scegli "No" se pensi di reinstallarlo e vuoi ritrovarli.',
         mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
    begin
      DeleteFile(ExpandConstant('{app}\LiveCount.settings.xml'));
      DelTree(ExpandConstant('{userappdata}\LiveCount'), True, True, True);
      RemoveDir(ExpandConstant('{app}'));
    end;
  end;
end;
