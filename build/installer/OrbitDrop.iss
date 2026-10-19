; OrbitDrop installer (Inno Setup 6). Per-user install: no administrator rights required.
; Built by build/package.ps1, which passes AppVersion, NumericVersion, SourceDir and OutputDir.

#ifndef AppVersion
  #define AppVersion "0.2.0-alpha"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.2.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts"
#endif

[Setup]
AppId={{8F3C2E5A-7B1D-4C9E-A6F2-0D4B9E7C1A35}
AppName=OrbitDrop
AppVersion={#AppVersion}
AppVerName=OrbitDrop {#AppVersion}
AppPublisher=OrbitDrop contributors
AppCopyright=© 2026 Mohammed Essam and OrbitDrop contributors · MIT License
VersionInfoVersion={#NumericVersion}
VersionInfoProductName=OrbitDrop
VersionInfoDescription=OrbitDrop Setup
DefaultDirName={autopf}\OrbitDrop
DefaultGroupName=OrbitDrop
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=OrbitDrop-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\src\OrbitDrop.UI\Assets\orbitdrop.ico
UninstallDisplayIcon={app}\OrbitDrop.exe
UninstallDisplayName=OrbitDrop
LicenseFile=..\..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Ask to close a running OrbitDrop (mutex created by SingleInstance) instead of failing to overwrite it.
AppMutex=OrbitDrop.Running
CloseApplications=yes
; Ends with SHChangeNotify(SHCNE_ASSOCCHANGED) so Explorer and Start drop cached icons. Without it, an upgrade
; that changes the app icon keeps showing the old one (the cache is keyed by the unchanged exe path).
ChangesAssociations=yes

[Tasks]
Name: "startup"; Description: "Start OrbitDrop when I sign in to Windows"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Options:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\OrbitDrop"; Filename: "{app}\OrbitDrop.exe"; Comment: "Convert, edit and manage files without opening an app"
Name: "{autodesktop}\OrbitDrop"; Filename: "{app}\OrbitDrop.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OrbitDrop"; \
  ValueData: """{app}\OrbitDrop.exe"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\OrbitDrop.exe"; Description: "Start OrbitDrop now"; Flags: nowait postinstall skipifsilent
; In-app updates run Setup silently with /RELAUNCH=1: start the new version once its files are in place.
Filename: "{app}\OrbitDrop.exe"; Parameters: "--updated"; Flags: nowait; Check: IsUpdateRelaunch

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im OrbitDrop.exe /f"; Flags: runhidden; RunOnceId: "StopOrbitDrop"

[Code]
function IsUpdateRelaunch: Boolean;
begin
  Result := WizardSilent and (ExpandConstant('{param:RELAUNCH|0}') = '1');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    { The app can enable "launch at sign-in" itself, so always remove the value. }
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'OrbitDrop');
    { Logs are machine-local and only useful to the installed copy. }
    DelTree(ExpandConstant('{localappdata}\OrbitDrop'), True, True, True);
    { Settings are kept unless the user asks (silent uninstalls keep them). }
    if (not UninstallSilent) and
       (MsgBox('Also remove your OrbitDrop settings?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(ExpandConstant('{userappdata}\OrbitDrop'), True, True, True);
  end;
end;
