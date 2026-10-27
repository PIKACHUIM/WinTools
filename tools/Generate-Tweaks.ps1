# 生成优化项：把下面的紧凑表展开成 tweaks-extra.json 并合并（保留原有条目）
# 每行：id|分组|中文名|英文名|风险|Hive|注册表路径|值名|类型|值
$rows = @'
telemetry-off|Privacy|关闭诊断遥测|Disable telemetry|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\DataCollection|AllowTelemetry|DWord|0
advertising-id-off|Privacy|关闭广告 ID|Disable advertising ID|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo|Enabled|DWord|0
activity-feed-off|Privacy|关闭活动历史|Disable activity history|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy|PublishUserActivities|DWord|0
activity-upload-off|Privacy|停止上传活动记录|Stop uploading activities|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy|UploadUserActivities|DWord|0
tailored-experiences-privacy-off|Privacy|关闭定制体验|Disable tailored experiences|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy|TailoredExperiencesWithDiagnosticDataEnabled|DWord|0
feedback-notifications-off|Privacy|关闭反馈提示|Disable feedback prompts|Low|HKCU|SOFTWARE\Microsoft\Siuf\Rules|NumberOfSIUFInPeriod|DWord|0
online-speech-off|Privacy|关闭在线语音识别|Disable online speech recognition|Low|HKCU|SOFTWARE\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy|HasAccepted|DWord|0
inking-personalization-off|Privacy|关闭手写数据收集|Disable inking data collection|Low|HKCU|SOFTWARE\Microsoft\InputPersonalization|RestrictImplicitTextCollection|DWord|1
ink-collection-off|Privacy|关闭墨迹收集|Disable ink collection|Low|HKCU|SOFTWARE\Microsoft\InputPersonalization|RestrictImplicitInkCollection|DWord|1
location-off|Privacy|关闭定位|Disable location|Medium|HKLM|SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors|DisableLocation|DWord|1
camera-off|Privacy|关闭相机|Disable camera|Medium|HKLM|SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors|DisableCamera|DWord|1
microphone-off|Privacy|关闭麦克风|Disable microphone|Medium|HKLM|SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors|DisableMicrophone|DWord|1
cloud-search-off|Privacy|关闭云端搜索|Disable cloud search|Low|HKCU|SOFTWARE\Policies\Microsoft\Windows\Windows Search|AllowCloudSearch|DWord|0
copilot-off|Privacy|关闭 Copilot|Disable Copilot|Low|HKCU|SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot|TurnOffWindowsCopilot|DWord|1
recall-off|Privacy|关闭召回快照|Disable Recall snapshots|Low|HKCU|SOFTWARE\Policies\Microsoft\Windows\WindowsAI|DisableAIDataAnalysis|DWord|1
suggested-apps-off|Privacy|关闭开始菜单推荐应用|Disable suggested apps|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager|SubscribedContent-338388Enabled|DWord|0
tips-off|Privacy|关闭使用提示|Disable tips and suggestions|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager|SoftLandingEnabled|DWord|0
lock-spotlight-off|Privacy|关闭锁屏聚焦|Disable lock screen spotlight|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager|RotatingLockScreenEnabled|DWord|0
settings-sync-off|Privacy|关闭设置同步|Disable settings sync|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\SettingSync|DisableSettingSync|DWord|2
recent-docs-off|Privacy|不记录最近文档|Stop tracking recent documents|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|Start_TrackDocs|DWord|0
recent-programs-off|Privacy|不记录最近程序|Stop tracking recent programs|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|Start_TrackProgs|DWord|0
wifi-sense-off|Privacy|关闭 WiFi 感知|Disable Wi-Fi Sense|Low|HKLM|SOFTWARE\Microsoft\WcmSvc\WiFiSense|AutoConnectAllowedOEM|DWord|0
file-ext-on|Explorer|显示文件扩展名|Show file extensions|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|HideFileExt|DWord|0
hidden-files-on|Explorer|显示隐藏文件|Show hidden files|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|Hidden|DWord|1
compact-mode-on|Explorer|紧凑视图|Compact view|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|UseCompactMode|DWord|1
open-this-pc|Explorer|打开此电脑|Open to This PC|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|LaunchTo|DWord|1
taskbar-left|Explorer|任务栏左对齐|Align taskbar to the left|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|TaskbarAl|DWord|0
search-box-off|Explorer|隐藏任务栏搜索框|Hide taskbar search box|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Search|SearchboxTaskbarMode|DWord|0
taskview-off|Explorer|隐藏任务视图|Hide Task View button|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|ShowTaskViewButton|DWord|0
widgets-off|Explorer|隐藏小组件|Hide Widgets|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|TaskbarDa|DWord|0
chat-off|Explorer|隐藏聊天|Hide Chat|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|TaskbarMn|DWord|0
people-off|Explorer|隐藏人脉|Hide People|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|PeopleBand|DWord|0
seconds-clock|Explorer|任务栏显示秒|Show seconds in clock|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|ShowSecondsInSystemClock|DWord|1
end-task-on|Explorer|任务栏结束任务|Enable End task|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|TaskbarEndTask|DWord|1
battery-percent|Explorer|显示电池百分比|Show battery percentage|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|TaskbarBatteryPercent|DWord|1
low-disk-warning-off|Explorer|关闭磁盘空间警告|Disable low disk warnings|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer|NoLowDiskSpaceChecks|DWord|1
drive-letters-first|Explorer|盘符显示在前|Show drive letters first|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer|ShowDriveLettersFirst|DWord|4
separate-process|Explorer|资源管理器独立进程|Run Explorer in its own process|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|SeparateProcess|DWord|1
autorun-off|Explorer|禁用自动播放|Disable AutoPlay|Medium|HKLM|SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer|NoDriveTypeAutoRun|DWord|255
menu-delay|Performance|加快菜单弹出|Faster menus|Low|HKCU|Control Panel\Desktop|MenuShowDelay|String|20
kill-timeout|Performance|加快服务关闭|Faster service shutdown|Low|HKCU|Control Panel\Desktop|WaitToKillServiceTimeout|String|2000
startup-delay-off|Performance|关闭启动延迟|Disable startup delay|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Serialize|StartupDelayInMSec|DWord|0
transparency-off|Performance|关闭透明效果|Disable transparency|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize|EnableTransparency|DWord|0
animations-off|Performance|关闭窗口动画|Disable window animations|Low|HKCU|Control Panel\Desktop\WindowMetrics|MinAnimate|String|0
taskbar-animations-off|Performance|关闭任务栏动画|Disable taskbar animations|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|TaskbarAnimations|DWord|0
aero-shake-off|Performance|关闭窗口抖动最小化|Disable Aero Shake|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|NoWindowMinimizingShortcuts|DWord|1
snap-assist-off|Performance|关闭贴靠建议|Disable Snap Assist suggestions|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|SnapAssist|DWord|0
mouse-accel-off|Performance|关闭鼠标加速|Disable mouse acceleration|Low|HKCU|Control Panel\Mouse|MouseSpeed|String|0
scroll-lines|Performance|滚轮每次 3 行|Scroll 3 lines per notch|Low|HKCU|Control Panel\Desktop|WheelScrollLines|String|3
long-paths-on|System|启用长路径|Enable long paths|Low|HKLM|SYSTEM\CurrentControlSet\Control\FileSystem|LongPathsEnabled|DWord|1
numlock-on|System|开机启用数字键盘|Enable Num Lock at sign-in|Low|HKCU|Control Panel\Keyboard|InitialKeyboardIndicators|String|2
bsod-details|System|蓝屏显示详细信息|Verbose blue screens|Low|HKLM|SYSTEM\CurrentControlSet\Control\CrashControl|DisplayParameters|DWord|1
remote-assistance-off|System|关闭远程协助|Disable Remote Assistance|Medium|HKLM|SYSTEM\CurrentControlSet\Control\Remote Assistance|fAllowToGetHelp|DWord|0
rdp-off|System|关闭远程桌面|Disable Remote Desktop|Medium|HKLM|SYSTEM\CurrentControlSet\Control\Terminal Server|fDenyTSConnections|DWord|1
fast-startup-off|System|关闭快速启动|Disable Fast Startup|Low|HKLM|SYSTEM\CurrentControlSet\Control\Session Manager\Power|HiberbootEnabled|DWord|0
hibernate-off|System|关闭休眠|Disable hibernation|Low|HKLM|SYSTEM\CurrentControlSet\Control\Power|HibernateEnabled|DWord|0
modern-standby-off|System|关闭新式待机|Disable modern standby|Medium|HKLM|SYSTEM\CurrentControlSet\Control\Power|PlatformAoAcOverride|DWord|0
smb1-off|Network|关闭 SMB1|Disable SMB1|Low|HKLM|SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters|SMB1|DWord|0
llmnr-off|Network|关闭 LLMNR|Disable LLMNR|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows NT\DNSClient|EnableMulticast|DWord|0
netbios-off|Network|禁用 NetBIOS|Disable NetBIOS|Medium|HKLM|SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces|NetbiosOptions|DWord|2
wifi-sense-net-off|Network|关闭热点共享|Disable hosted network|Low|HKLM|SOFTWARE\Microsoft\WlanSvc\AnqpCache|OsuRegistrationEnabled|DWord|0
delivery-optimization-off|Update|关闭更新 P2P|Disable Delivery Optimization P2P|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization|DODownloadMode|DWord|0
driver-updates-off|Update|不随更新装驱动|Exclude drivers from updates|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate|ExcludeWUDriversInQualityUpdate|DWord|1
defer-feature-updates|Update|推迟功能更新|Defer feature updates|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate|DeferFeatureUpdates|DWord|1
defer-quality-updates|Update|推迟质量更新|Defer quality updates|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate|DeferQualityUpdates|DWord|1
no-auto-restart|Update|更新后不自动重启|Do not auto-restart after updates|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU|NoAutoRebootWithLoggedOnUsers|DWord|1
update-notifications-off|Update|关闭更新提示|Disable update notifications|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU|NoAutoUpdate|DWord|0
gamebar-off|Gaming|关闭游戏栏|Disable Game Bar|Low|HKCU|SOFTWARE\Microsoft\GameBar|ShowStartupPanel|DWord|0
game-capture-off|Gaming|关闭后台录制|Disable background recording|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR|AppCaptureEnabled|DWord|0
game-dvr-policy-off|Gaming|关闭游戏 DVR|Disable Game DVR|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\GameDVR|AllowGameDVR|DWord|0
game-mode-toggle-on|Gaming|启用游戏模式|Enable Game Mode|Low|HKCU|SOFTWARE\Microsoft\GameBar|AutoGameModeEnabled|DWord|1
fullscreen-opt-off|Gaming|关闭全屏优化|Disable fullscreen optimizations|Low|HKCU|System\GameConfigStore|GameDVR_FSEBehaviorMode|DWord|2
mouse-trails-off|Gaming|关闭鼠标轨迹|Disable mouse trails|Low|HKCU|Control Panel\Mouse|MouseTrails|String|0
sticky-keys-off|System|关闭粘滞键提示|Disable Sticky Keys prompts|Low|HKCU|Control Panel\Accessibility\StickyKeys|Flags|String|506
filter-keys-off|System|关闭筛选键提示|Disable Filter Keys prompts|Low|HKCU|Control Panel\Accessibility\Keyboard Response|Flags|String|122
toggle-keys-off|System|关闭切换键提示|Disable Toggle Keys prompts|Low|HKCU|Control Panel\Accessibility\ToggleKeys|Flags|String|58
screensaver-off|System|禁用屏保|Disable screen saver|Low|HKCU|Control Panel\Desktop|ScreenSaveActive|String|0
auto-maintenance-off|System|关闭自动维护|Disable automatic maintenance|Low|HKLM|SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\Maintenance|MaintenanceDisabled|DWord|1
error-reporting-off|System|关闭错误报告|Disable error reporting|Low|HKLM|SOFTWARE\Microsoft\Windows\Windows Error Reporting|Disabled|DWord|1
bing-start-off|Explorer|开始菜单关闭网络搜索|Disable web results in Start|Low|HKCU|SOFTWARE\Policies\Microsoft\Windows\Explorer|DisableSearchBoxSuggestions|DWord|1
old-context-menu|Explorer|经典右键菜单|Classic context menu|Low|HKCU|SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32|(Default)|String|
thumbnail-cache-off|Performance|关闭缩略图缓存|Disable thumbnail cache|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|DisableThumbnailCache|DWord|1
menu-show-delay-zero|Performance|菜单零延迟|Zero menu delay|Low|HKCU|Control Panel\Desktop|MenuShowDelay|String|0
explorer-restart|Performance|资源管理器单独进程|Separate Explorer process|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced|SeparateProcess|DWord|1
cloud-clipboard-off|Privacy|关闭云剪贴板|Disable cloud clipboard|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\System|AllowClipboardHistory|DWord|0
clipboard-sync-off|Privacy|关闭剪贴板跨设备同步|Disable clipboard sync|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\System|AllowCrossDeviceClipboard|DWord|0
edge-first-run-off|Privacy|跳过 Edge 首次运行|Skip Edge first run|Low|HKLM|SOFTWARE\Policies\Microsoft\Edge|HideFirstRunExperience|DWord|1
edge-sidebar-off|Privacy|关闭 Edge 侧边栏|Disable Edge sidebar|Low|HKLM|SOFTWARE\Policies\Microsoft\Edge|HubsSidebarEnabled|DWord|0
office-telemetry-off|Privacy|关闭 Office 遥测|Disable Office telemetry|Low|HKLM|SOFTWARE\Policies\Microsoft\Office\Common\ClientTelemetry|DisableTelemetry|DWord|1
vs-telemetry-off|Privacy|关闭 VS 遥测|Disable Visual Studio telemetry|Low|HKCU|SOFTWARE\Microsoft\VisualStudio\Telemetry|DisableTelemetry|DWord|1
crash-dump-off|System|关闭崩溃转储|Disable crash dumps|Medium|HKLM|SYSTEM\CurrentControlSet\Control\CrashControl|CrashDumpEnabled|DWord|0
magnifier-off|System|关闭放大镜启动|Disable Magnifier at sign-in|Low|HKCU|SOFTWARE\Microsoft\ScreenMagnifier|RunningState|DWord|0
narrator-off|System|关闭讲述人启动|Disable Narrator at sign-in|Low|HKCU|SOFTWARE\Microsoft\Narrator\NoRoam|RunningState|DWord|0
store-auto-update-off|Update|关闭商店自动更新|Disable Store auto updates|Low|HKLM|SOFTWARE\Policies\Microsoft\WindowsStore|AutoDownload|DWord|2
onedrive-sync-off|Privacy|关闭 OneDrive 文件同步|Disable OneDrive file sync|Medium|HKLM|SOFTWARE\Policies\Microsoft\Windows\OneDrive|DisableFileSyncNGSC|DWord|1
cortana-off|Privacy|关闭 Cortana|Disable Cortana|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\Windows Search|AllowCortana|DWord|0
pca-off|System|关闭程序兼容助手|Disable Program Compatibility Assistant|Low|HKCU|SOFTWARE\Policies\Microsoft\Windows\AppCompat|DisablePCA|DWord|1
touch-feedback-off|System|关闭触摸反馈音|Disable touch feedback sounds|Low|HKCU|Control Panel\Cursors|ContactVisualization|DWord|0
defrag-notify-off|System|关闭碎片整理通知|Disable defrag notifications|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Notifications\Settings\Microsoft.Windows.Defrag|Enabled|DWord|0
lock-camera-off|Privacy|锁屏禁用相机|Disable camera on lock screen|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Lock Screen|SlideshowEnabled|DWord|0
wifi-hotspot-off|Network|关闭移动热点|Disable mobile hotspot|Low|HKLM|SOFTWARE\Microsoft\WlanSvc\AnqpCache|EnableHostedNetwork|DWord|0
fax-off|System|关闭传真服务|Disable Fax service|Low|HKLM|SOFTWARE\Microsoft\Fax\Devices|ArchiveFolder|String|
smartscreen-on|System|开启 SmartScreen|Enable SmartScreen|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer|SmartScreenEnabled|String|Warn
diagtrack-off|Privacy|关闭诊断跟踪服务|Stop DiagTrack service|Low|HKLM|SYSTEM\CurrentControlSet\Services\DiagTrack|Start|DWord|4
dmwappush-off|Privacy|关闭 WAP 推送服务|Stop dmwappushservice|Low|HKLM|SYSTEM\CurrentControlSet\Services\dmwappushservice|Start|DWord|4
mapsbroker-off|Privacy|关闭地图服务|Stop MapsBroker|Low|HKLM|SYSTEM\CurrentControlSet\Services\MapsBroker|Start|DWord|4
wsearch-off|Performance|关闭搜索索引服务|Stop Windows Search indexer|Medium|HKLM|SYSTEM\CurrentControlSet\Services\WSearch|Start|DWord|4
remoteregistry-off|System|关闭远程注册表服务|Stop Remote Registry|Low|HKLM|SYSTEM\CurrentControlSet\Services\RemoteRegistry|Start|DWord|4
faxsvc-off|System|关闭传真服务启动|Disable Fax service startup|Low|HKLM|SYSTEM\CurrentControlSet\Services\Fax|Start|DWord|4
tablet-mode-off|System|关闭平板模式|Disable tablet mode|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\ImmersiveShell|TabletMode|DWord|0
background-apps-off|Privacy|禁止后台应用|Disable background apps|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications|GlobalUserDisabled|DWord|1
ceip-off|Privacy|退出客户体验计划|Leave Customer Experience Program|Low|HKLM|SOFTWARE\Microsoft\SQMClient\Windows|CEIPEnable|DWord|0
appcompat-ait-off|Privacy|关闭应用兼容遥测|Disable AppCompat telemetry|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\AppCompat|AITEnable|DWord|0
appcompat-inventory-off|Privacy|关闭应用清单收集|Disable inventory collector|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\AppCompat|DisableInventory|DWord|1
appcompat-uar-off|Privacy|关闭步骤记录器|Disable Steps Recorder|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\AppCompat|DisableUAR|DWord|1
defender-samples-off|Privacy|不上传样本|Do not submit samples|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows Defender\Spynet|SubmitSamplesConsent|DWord|2
defender-archive-off|Performance|关闭压缩包扫描|Disable archive scanning|Medium|HKLM|SOFTWARE\Policies\Microsoft\Windows Defender\Scan|DisableArchiveScanning|DWord|1
defender-removable-off|Performance|关闭可移动盘扫描|Disable removable drive scanning|Medium|HKLM|SOFTWARE\Policies\Microsoft\Windows Defender\Scan|DisableRemovableDriveScanning|DWord|1
defender-cpu-limit|Performance|限制杀毒 CPU 占用|Cap antivirus CPU usage|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows Defender\Scan|AvgCPULoadFactor|DWord|20
lock-screen-off|Privacy|关闭锁屏界面|Disable lock screen|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\Personalization|NoLockScreen|DWord|1
compressed-color-on|Explorer|显示压缩文件颜色|Show compressed file colors|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer|ShowCompColor|DWord|1
vbs-off|Performance|关闭基于虚拟化的安全|Disable VBS|High|HKLM|SYSTEM\CurrentControlSet\Control\DeviceGuard|EnableVirtualizationBasedSecurity|DWord|0
edge-superdrag-off|Privacy|关闭 Edge 超级拖放|Disable Edge super drag drop|Low|HKLM|SOFTWARE\Policies\Microsoft\Edge|SuperDragDropEnabled|DWord|0
edge-background-off|Performance|关闭 Edge 后台运行|Disable Edge background mode|Low|HKLM|SOFTWARE\Policies\Microsoft\Edge|BackgroundModeEnabled|DWord|0
edge-prelaunch-off|Performance|关闭 Edge 预启动|Disable Edge prelaunch|Low|HKLM|SOFTWARE\Policies\Microsoft\Edge|AllowPrelaunch|DWord|0
store-auto-download-off|Update|关闭商店自动下载|Disable Store auto download|Low|HKCU|SOFTWARE\Microsoft\WindowsStore|AutoDownload|DWord|2
cortana-consent-off|Privacy|关闭 Cortana 授权|Disable Cortana consent|Low|HKCU|SOFTWARE\Microsoft\Windows\CurrentVersion\Search|CortanaConsent|DWord|0
telemetry-feedback-off|Privacy|关闭遥测反馈提示|Disable telemetry feedback prompts|Low|HKLM|SOFTWARE\Policies\Microsoft\Windows\DataCollection|DoNotShowFeedbackNotifications|DWord|1
'@

$entries = New-Object System.Collections.Generic.List[object]

foreach ($line in ($rows -split "`n")) {
    $line = $line.Trim()
    if ($line.Length -eq 0) { continue }

    $f = $line.Split('|')
    if ($f.Count -lt 10) { throw "行格式不对：$line" }

    $id, $group, $zh, $en, $risk, $hive, $path, $name, $kind, $value = $f[0..9]

    $op = [ordered]@{
        type  = 'RegSet'
        hive  = $hive
        path  = $path
        name  = $name
        kind  = $kind
        value = $value
    }

    $entries.Add([ordered]@{
        id           = $id
        group        = $group
        recommended  = ($risk -eq 'Low')
        name         = [ordered]@{ 'zh-Hans' = $zh; 'en-US' = $en }
        description  = [ordered]@{ 'zh-Hans' = $zh; 'en-US' = $en }
        risk         = $risk
        detect       = @($op)
        apply        = @($op)
    })
}

$target = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/WinTools.Infrastructure/Rules/tweaks-extra.json'
$existing = @()
if (Test-Path $target) {
    # 根数组会被 ConvertFrom-Json 返回成「一个数组对象」，必须展开，否则会写出嵌套数组
    $existing = @(Get-Content $target -Raw -Encoding UTF8 | ConvertFrom-Json | ForEach-Object { $_ })
}

$existingIds = @($existing | ForEach-Object { $_.id })
$added = @($entries | Where-Object { $existingIds -notcontains $_.id })

$all = @($existing) + $added
$json = ConvertTo-Json -InputObject $all -Depth 8
[IO.File]::WriteAllText($target, $json, (New-Object Text.UTF8Encoding($false)))

Write-Host "已有 $($existing.Count) 项，新增 $($added.Count) 项，合计 $($all.Count) 项"
