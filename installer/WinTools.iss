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
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 框架依赖包：目标机需已安装 .NET 10 Desktop Runtime，安装程序会检查
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

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
