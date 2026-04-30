; PrintAgent.iss -- Inno Setup script for PrintAgent
; Build with: iscc.exe PrintAgent.iss

#define MyAppName "PrintAgent"
#define MyAppVersion "0.1.0"
#define MyAppExeName "PrintAgent.exe"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\PrintAgent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputBaseFilename=PrintAgentSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\agent\PrintAgent\bin\Release\net8.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer PrintAgent"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden; RunOnceId: "killprintagent"
Filename: "powershell.exe"; Parameters: "-NoProfile -Command ""Get-ChildItem -Path Cert:\CurrentUser\Root | Where-Object Subject -eq 'CN=localhost' | Where-Object NotAfter -gt (Get-Date).AddYears(5) | Remove-Item -Force"""; Flags: runhidden; RunOnceId: "removecert"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\PrintAgent"
