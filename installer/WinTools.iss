; WinTools 安装包脚本（Inno Setup 6）
; 由 tools/build-installer.ps1 调用，通过 /D 传入版本号与源目录

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\framework"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\publish"
#endif

#define AppName "WinTools"
#define AppPublisher "PIKACHUIM"
#define AppUrl "https://github.com/PIKACHUIM/WinTools"
#define AppExe "WinTools.exe"

[Setup]
AppId={{8C1B4C7E-3F52-4F0B-9E6D-2A7C5B9E4D31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename={#AppName}-{#AppVersion}-setup-win-x64
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 程序清单已要求管理员，安装时同样需要
PrivilegesRequired=admin
MinVersion=10.0.17763
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} 安装程序
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
; 简体中文语言包不在 Inno Setup 默认安装里，随仓库提供（installer/languages/）；
; 万一文件被删掉，就退回只有英文，不让整个打包因此失败
#ifexist "languages\ChineseSimplified.isl"
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; 启动方式二选一。程序清单要求管理员权限，直接启动每次都会弹 UAC；
; 计划任务方式由任务以最高权限拉起，启动时不再询问。
Name: "launch_task"; Description: "免 UAC 启动（创建计划任务，推荐）"; GroupDescription: "启动方式："; Flags: exclusive
Name: "launch_direct"; Description: "直接启动（每次询问管理员权限）"; GroupDescription: "启动方式："; Flags: exclusive unchecked

[Files]
; 框架依赖包：目标机需已安装 .NET 10 Desktop Runtime，安装程序会检查
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; 免 UAC 启动脚本（仅当选了计划任务方式）
Source: "launch.vbs"; DestDir: "{app}"; Flags: ignoreversion; Tasks: launch_task

[Icons]
; 计划任务方式：用 wscript 静默执行 launch.vbs，不闪控制台
Name: "{group}\{#AppName}"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\launch.vbs"""; Tasks: launch_task
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: launch_direct
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{sys}\wscript.exe"; Parameters: """{app}\launch.vbs"""; Tasks: desktopicon and launch_task
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon and launch_direct

[Run]
; 注册计划任务：安装器本身已是管理员，这一步不会再弹 UAC。
; 任务没有触发器，只能被 launch.vbs 里的 schtasks /run 唤醒。
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -WindowStyle Hidden -Command ""Register-ScheduledTask -TaskName 'WinTools' -Action (New-ScheduledTaskAction -Execute '{app}\{#AppExe}') -RunLevel Highest -Force | Out-Null"""; Flags: runhidden waituntilterminated; Tasks: launch_task; StatusMsg: "正在配置免 UAC 启动…"

Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Tasks: launch_direct
Filename: "{sys}\wscript.exe"; Parameters: """{app}\launch.vbs"""; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Tasks: launch_task

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/delete /tn ""WinTools"" /f"; Flags: runhidden; RunOnceId: "DelLaunchTask"

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';

function IsDotNet10Installed(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  Version: String;
begin
  Result := False;
  if not RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    Exit;

  for I := 0 to GetArrayLength(Names) - 1 do
  begin
    Version := Names[I];
    if Pos('10.', Version) = 1 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet10Installed() then
  begin
    if MsgBox('未检测到 .NET 10 Desktop Runtime，程序无法启动。' + #13#10 +
              '是否现在打开下载页面？', mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);

    // 不强制中断：允许用户先装运行时再启动程序
  end;
end;
