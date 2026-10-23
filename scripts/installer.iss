#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif
#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef ReleaseDir
  #error ReleaseDir is required
#endif

[Setup]
AppId={{012D7D96-1B63-4416-9973-77E054AF8E00}
AppName=Muse Desktop
AppVersion={#AppVersion}
AppPublisher=tszmzz1026
AppPublisherURL=https://github.com/tszmzz1026/muse-desktop
AppSupportURL=https://github.com/tszmzz1026/muse-desktop/issues
AppUpdatesURL=https://github.com/tszmzz1026/muse-desktop/releases
DefaultDirName={localappdata}\Programs\MuseDesktop
DefaultGroupName=Muse Desktop
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#ReleaseDir}
OutputBaseFilename=Muse-Desktop-{#AppVersion}-Setup
SetupIconFile={#SourceDir}\muse.ico
UninstallDisplayIcon={app}\Muse.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
AppMutex=Local\Muse.Desktop.Instance
CloseApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#SourceDir}\Muse.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Microsoft.Web.WebView2.Core.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\Microsoft.Web.WebView2.WinForms.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\WebView2Loader.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\THIRD_PARTY_NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{group}\Muse Desktop"; Filename: "{app}\Muse.exe"
Name: "{userdesktop}\Muse Desktop"; Filename: "{app}\Muse.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Muse.exe"; Description: "Open Muse Desktop"; Flags: nowait postinstall skipifsilent

[Code]
function HasWebView2Runtime: Boolean;
var
  Version: String;
  Key: String;
begin
  Key := 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := (RegQueryStringValue(HKCU32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'))
    or (RegQueryStringValue(HKLM32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if not HasWebView2Runtime then
  begin
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := 'Unable to start Microsoft WebView2 Runtime setup. Check your internet connection and try again.'
    else if not HasWebView2Runtime then
      Result := 'Microsoft WebView2 Runtime could not be installed. Install it from Microsoft and run setup again.';
  end;
end;
