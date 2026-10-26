; Inno Setup 6 script for Screen Recorder.
; Build the app first:   dotnet publish src/ScreenRecorder.App -p:PublishProfile=win-x64
; Then compile:          "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\ScreenRecorder.iss
; Output:                artifacts\installer\ScreenRecorder-Setup-<version>.exe

#define AppName "Screen Recorder"
#define AppVersion "1.0.0"
#define AppExe "ScreenRecorder.exe"
#define PublishDir "..\artifacts\publish"

[Setup]
AppId={{6C1E5A2B-7F2D-4E8B-9A43-2B7D5C0E9F11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=ScreenRecorder
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=ScreenRecorder-Setup-{#AppVersion}
SetupIconFile=..\src\ScreenRecorder.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\ScreenRecorderLib.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[UninstallDelete]
; Error reports written by the app next to its executable.
Type: filesandordirs; Name: "{app}\logs"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// The app is framework-dependent: it needs the .NET 8 Desktop Runtime (x64).
function IsDesktopRuntime8Installed(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDesktopRuntime8Installed() then
    Result := MsgBox('Screen Recorder needs the .NET 8 Desktop Runtime (x64), which was not found.' + #13#10 + #13#10 +
      'Download it from https://dotnet.microsoft.com/download/dotnet/8.0 and run setup again, ' +
      'or click Yes to install anyway.', mbConfirmation, MB_YESNO) = IDYES;
end;
