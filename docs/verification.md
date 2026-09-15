# 2026-09-15 验证记录

## 已验证

- 官方 NuGet 依赖恢复成功；Rider .NET SDK 8.0.101 可完整编译真实 WinRT 源码。
- Release/xUnit：27/27 通过，零失败、零跳过。新增覆盖精确来源选择、默认关闭、旧 JSON 迁移、同名来源误迁移防护、保存/刷新草稿、权限恢复历史基线及显示前筛选。
- 离线行为检查：42/42 通过。
- 预览版编译及运行通过；最终自包含完整版以 --smoke --capture 实际启动并退出，退出码 0。
- 卡顿回归：移除每 2 秒 SetWindowPos 后，5 条短中文测试约 176 FPS、长中文/表情约 137 FPS；窗口高度从 1152 缩小为 306，长文本长帧（>16.7ms）降为 1 次。
- 完整版覆盖层：Topmost=true、ClickThrough=true、NoActivate=true，5 条显示通道；测试结束 QueueActive=0、OverlayActive=0、Pending=0。检查了设置页渲染。
- PowerShell 脚本通过 Windows PowerShell 5.1 语法解析；中文脚本按 UTF-8 BOM 保存。
- MSIX 1.1.1.0 x64 构建与 MakeAppx 清单验证成功，包含 .NETCore/WindowsDesktop 8.0.31 自包含运行时。
- SignTool 成功签名。本机专用证书为 CN=NotificationBarrage.Dev，CurrentUser\My 中私钥不可导出；仅导出公钥 CER。开发签名不等于 Microsoft Store 审核或公有 CA 信任。
- 本机 Windows PowerShell Toast 探针成功发出固定测试通知，并且只按自己的 tag/group 查询确认其存在于来源历史；不读取或记录其他历史正文。

## 安装与接收状态

用户已确认 Windows UAC。项目证书已加入 LocalMachine\TrustedPeople；SignTool verify /pa 成功，签名状态 Valid。MSIX 1.1.1.0 已安装到原登录用户，包状态 Ok，程序已从 WindowsApps 目录实际启动。已读取并保存 5 个通知应用来源（含 Windows PowerShell），来源默认全部关闭，通知读取权限可用。

用户已在设置中勾选 Windows PowerShell 并保存。随后发送带唯一标记的系统 Toast，确认 PresentInSenderHistory=true；只在已安装程序的覆盖层搜索本次固定测试文本，FoundInInstalledOverlay=true，MaximumConcurrentMatches=1，RemovedAfterAnimation=true。证据保存在 artifacts/msix/reception-check.json。该结果证明真实 Windows 通知接收及弹幕显示链路，尚不代表外部通讯软件来信已验收。

以下尚未确认：
- Telegram/Teams/Outlook/QQ 等外部应用真实来信。
- 实际游戏、权限撤销/恢复、开机启动、不同 DPI 与显示器布局的集成验收。

源码/单元测试或脚本 PresentInSenderHistory=true 不替代外部来信和实际游戏验收。本轮系统 Toast 的显示结论另外有已安装程序 UI 自动化证据。

## 产物与复现

- 签名包：artifacts/msix/NotificationBarrage-1.1.1.0-x64.msix
- 公钥：artifacts/msix/NotifyBar-Dev.cer
- 构建记录：artifacts/msix/build-info.json 与 build.log
- 签名包 SHA256：A86321DD44B57E78765FB0C8152EE93EFB3A3981FCD40266E68CCBBDE1A4AC2B
- 全部产物和本机证书都被 Git 忽略。
- 在 Windows PowerShell 5.1 运行 scripts/Install-DevMsix.ps1 -TrustCertificate。
- 从开始菜单打开 NotifyBar，连接通知并在系统提示允许。
- 执行 scripts/Send-TestNotification.ps1，刷新并勾选 Windows PowerShell，保存后再次执行脚本检查弹幕。
- 外部来信按 README 步骤单独验证。只使用 Windows 通知中心，不接入微信自有弹窗。

## 审查与实现说明

Task 1 由实现子代理完成，独立审查提出两个阻断问题：旧迁移依赖显示名、公开权限检查不重置快照。均已修复并增加回归测试；权限检查还会使正在获取的旧快照失效。后续修复和安装脚本由主代理实施、检查，未将主代理复核描述成独立子代理复审。

保留普通 WPF 覆盖层、主屏幕和无边框游戏的范围。原有 NotificationBarrage 程序集与配置身份不改，用户可见名称为 NotifyBar。证书仅受信任到 TrustedPeople，不导入 Root；提权仅用于导入该公钥，Add-AppxPackage 在原用户会话执行。

## 2026-09-16 正式版周期性卡顿修复

正式版才会触发的卡顿来自 Windows 通知监听路径：`StartAsync` 从 WPF UI 线程启动后台循环，`GetNotificationsAsync`、刷新闸门和唤醒等待完成后默认捕获 `DispatcherSynchronizationContext`。每次通知快照返回时，历史通知遍历、文本映射和快照更新因此回到 UI 线程，与覆盖层动画竞争合成；预览版没有真实监听，所以无法复现。中文文本不是主要原因。

修复将后台监听、通知快照、刷新等待、唤醒等待和停止等待统一改为 `ConfigureAwait(false)`；`StartAsync` 还通过 `Task.Run` 启动监听循环，避免首个 WinRT await 同步完成时在 UI 线程执行快照解析。`RequestAccessAsync` 仍保留 UI 上下文，因为它由设置页按钮直接调用；通知事件和来源事件继续通过 `AppHost.Dispatcher.BeginInvoke` 回到 UI 线程。

回归保护与结果：

- 离线检查会读取正式监听源并验证上述关键 await 均使用 `ConfigureAwait(false)`；修改前该检查按预期失败，修复后通过。
- `dotnet build NotificationBarrage.sln --no-restore -c Release`：0 警告、0 错误。
- `dotnet test NotificationBarrage.sln --no-build --no-restore -c Release`：27/27 通过。
- `scripts/Invoke-OfflineChecks.ps1`：49 项检查通过。

复现/验证命令（PowerShell）：

```powershell
$env:DOTNET_CLI_HOME = 'D:\notification-barrage\.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = 'D:\notification-barrage\.tools\packages'
$sdk = 'D:\JetBrains Rider 2024.1.5\lib\ReSharperHost\windows-x64\dotnet\dotnet.exe'
& $sdk build NotificationBarrage.sln --no-restore -c Release
& $sdk test NotificationBarrage.sln --no-build --no-restore -c Release
.\scripts\Invoke-OfflineChecks.ps1 -DotnetPath $sdk
```

## 2026-09-16 设置页滚动与视觉优化

设置页原先在页面滚动容器内又嵌套通知来源滚动容器，鼠标滚轮在内层列表边界容易被 WPF 截留，导致页面上下滚动不稳定。现在通知来源列表并入唯一的页面滚动容器，使用连续像素滚动；搜索框、位置下拉框、按钮、卡片间距和固定底栏也统一了深色主题。

- 离线检查增加唯一滚动容器回归断言，当前 `52` 项通过。
- Release 编译：0 警告、0 错误；xUnit：27/27 通过。
- MSIX `1.1.3.0` 签名并安装成功，包状态 `Ok`；安装版已启动并收到本机 Windows PowerShell 测试通知历史记录。
