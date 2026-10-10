#define AppName "hdr-tray"
#define AppPublisher "Yusef Ouda"
#define AppURL "https://github.com/YusefOuda/hdr-tray"
#define AppExeName "HdrTray.exe"

[Setup]
AppId={{6A1F0C2E-8B47-4D3A-9E15-C2D7B8F40A93}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={localappdata}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
; Restart Manager can't close a tray app; StopApp below does it
CloseApplications=no
OutputDir=publish
OutputBaseFilename=hdr-tray-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "publish\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userstartmenu}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Closes the running app before its files are replaced or deleted. --quit lets it exit cleanly
// (tray icon removed); versions older than --quit ignore it and are force-closed after 5 s.
const
  AppMutex = 'hdr-tray-{6A1F0C2E-8B47-4D3A-9E15-C2D7B8F40A93}';

procedure StopApp();
var
  ResultCode, I: Integer;
begin
  if not CheckForMutexes(AppMutex) then Exit;
  Exec(ExpandConstant('{app}\{#AppExeName}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  for I := 1 to 50 do
  begin
    if not CheckForMutexes(AppMutex) then Exit;
    Sleep(100);
  end;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/f /im {#AppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  for I := 1 to 20 do
  begin
    if not CheckForMutexes(AppMutex) then Exit;
    Sleep(100);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopApp();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopApp();
end;
