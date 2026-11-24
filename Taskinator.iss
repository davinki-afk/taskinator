; Compile with Inno Setup 5.6.1 (Unicode).
; Taskinator\ contains the extracted contents of Taskinator.zip.

[Setup]
AppId=Taskinator
AppName=Taskinator
AppVersion=1.0
VersionInfoVersion=1.0
VersionInfoTextVersion=1.0
VersionInfoDescription=DaVINCI.s23.xyz
DefaultDirName={pf}\Taskinator
DefaultGroupName=Taskinator
DisableProgramGroupPage=yes
PrivilegesRequired=admin
MinVersion=5.1sp3
AppMutex=Local\Taskinator.{{065353B4-79A4-45A4-B6B9-2D11F59B2503}
UninstallDisplayIcon={app}\Taskinator.exe
SetupIconFile=taskinator.ico
DisableWelcomePage=no
WizardImageFile=wizarddv.bmp
WizardSmallImageFile=minidv.bmp
OutputDir=Output
OutputBaseFilename=Taskinator-Setup
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.DotNetRequired=Taskinator requires Microsoft .NET Framework 4 (full) or a later 4.x version. Install it and run Taskinator Setup again.
russian.DotNetRequired=Для Taskinator требуется Microsoft .NET Framework 4 (полная версия) или более новая версия 4.x. Установите её и заново запустите установщик Taskinator.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
; Only imported sound data needs write access for standard users.
Name: "{app}\sounds\.imported"; Permissions: users-modify

[Files]
Source: "Taskinator\Taskinator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Taskinator\help\ru\Taskinator.chm"; DestDir: "{app}\help\ru"; Flags: ignoreversion
Source: "Taskinator\help\en\Taskinator.chm"; DestDir: "{app}\help\en"; Flags: ignoreversion
Source: "Taskinator\help\uk\Taskinator.chm"; DestDir: "{app}\help\uk"; Flags: ignoreversion
Source: "Taskinator\help\be\Taskinator.chm"; DestDir: "{app}\help\be"; Flags: ignoreversion
Source: "Taskinator\help\kk\Taskinator.chm"; DestDir: "{app}\help\kk"; Flags: ignoreversion
Source: "Taskinator\icons\*.png"; DestDir: "{app}\icons"; Flags: ignoreversion
Source: "Taskinator\lang\*.xml"; DestDir: "{app}\lang"; Flags: ignoreversion
Source: "Taskinator\sounds\default\*.wav"; DestDir: "{app}\sounds\default"; Flags: ignoreversion

[Icons]
Name: "{group}\Taskinator"; Filename: "{app}\Taskinator.exe"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,Taskinator}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Taskinator"; Filename: "{app}\Taskinator.exe"; WorkingDir: "{app}"; Tasks: desktopicon

; Taskinator creates %APPDATA%\Taskinator\lists.xml under the account
; running the application. Setup and Uninstall leave that file alone.
[Code]
const
  FrameworkKey = 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full';
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
  Executable: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, StartupKey, 'Taskinator', Command) then
    begin
      Executable := '"' + ExpandConstant('{app}\Taskinator.exe') + '"';
      if (CompareText(Command, Executable + ' --autostart') = 0) or
         (CompareText(Command, Executable) = 0) then
        RegDeleteValue(HKCU, StartupKey, 'Taskinator');
    end;
end;

function InitializeSetup(): Boolean;
var
  Installed: Cardinal;
  Release: Cardinal;
begin
  Result :=
    (RegQueryDWordValue(HKLM32, FrameworkKey, 'Install', Installed) and
      (Installed = 1)) or
    (RegQueryDWordValue(HKLM32, FrameworkKey, 'Release', Release) and
      (Release >= 378389));

  if not Result then
    SuppressibleMsgBox(CustomMessage('DotNetRequired'), mbError, MB_OK, IDOK);
end;
