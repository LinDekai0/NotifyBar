# NotifyBar

C# / .NET 8 WPF 桌面工具，把 QQ、微信发到 **Windows 通知中心** 的新消息显示成游戏画面上的滚动弹幕。

## 当前可运行的版本

双击 `artifacts/preview/NotificationBarrage.Preview.exe`，在设置窗口点击 **发送测试弹幕**。关闭设置窗口后，应用继续在托盘运行；右键托盘可暂停、恢复、测试或退出。调整参数后先保存，再测试。

**这是显示预览版，不接收真实系统通知。** 已验证预览编译、启动、弹幕队列、覆盖层样式及自动退出。完整通知监听源码和 MSIX 打包脚本已提供，但当前环境读取 Windows SDK 目录受限，提升权限又被自动审批服务的 429 错误阻断，尚未完成完整版编译、签名安装或真实 QQ/微信验收。

预览运行需要 Windows x64 和 .NET 8 Desktop Runtime。本机已检测到 Desktop Runtime 8.0.30。移动预览版时需一起复制整个 `artifacts/preview/` 目录，不能只复制 EXE。

## 功能与使用边界

- QQ / 微信来源开关、标题与正文显示、通知 ID 与短时间正文去重。
- 每秒最多接收 3 条，最多并发 5 条；待显示队列最多 100 条，溢出丢弃最旧消息。
- 暂停时保留最近 10 条；重新授权、重连或启动时，不回放通知中心里的旧消息。
- 主屏幕透明置顶窗口，不抢焦点、鼠标穿透；顶部、中部、底部及偏移可调。
- 设置、日志仅保存在本机；聊天正文和异常消息文本不写入日志，不联网上传。
- **窗口化和无边框全屏游戏**是首版支持目标。普通 WPF 置顶窗口不能保证盖住独占全屏游戏；请改用无边框全屏并实测。
- QQ / 微信必须真的把消息写入 Windows 通知中心；客户端聊天弹窗不一定属于系统通知。没有正文预览的通知无法还原内容。系统勿扰/专注模式、游戏模式和客户端版本会影响通知是否产生，需在目标电脑验证。
- 只支持主屏幕；不读取聊天窗口、不回复消息、不注入游戏进程。

## 开发、测试与启动

### 当前项目目录

在 `D:\notification-barrage` 目录打开 PowerShell，执行：

```powershell
Set-Location 'D:\notification-barrage'
.\scripts\Restore-Dependencies.ps1
```

项目已放在 D 盘根目录并包含独立 Git 仓库。恢复脚本自动查找 .NET 8 SDK（含本机 Rider SDK），使用官方 NuGet 源并运行 Release 构建及测试。若失败，查看 `artifacts/setup-logs/restore.log` 或 `test.log`，不必先逐个手工下载依赖。

### 微信没有出现在 Windows 通知设置中

这通常是客户端使用自己的消息弹窗，或者还没有注册/产生 Windows 通知；仅凭设置列表不能下最终结论。把微信放到后台，收到一条新消息后，Windows 11 按 `Win + N`（Windows 10 按 `Win + A`），检查那条消息是否进入通知中心。如果只有微信自己的弹窗，本应用在当前“只读取 Windows 通知中心”的范围内无法接收它，也无法还原客户端未提供的消息正文。

可在自己的 PowerShell 中运行以下只读诊断，查看客户端版本及通知注册线索（不读取聊天内容）：

```powershell
.\scripts\Get-NotificationSupport.ps1
```

代理沙箱的 HKCU 和开始菜单结果可能属于沙箱账户，不能代替你的登录账户检测。本机已检测到微信 `4.1.13.65`，但尚未验证该版本实际向通知中心写入消息。QQ 也须通过同样的实测。

### 常规命令

环境：Windows 10 1903+ / Windows 11 x64、.NET 8 SDK。完整编译首次需访问 NuGet 下载 Windows SDK .NET 引用及 xUnit 包。

在本目录运行：

```powershell
dotnet restore .\NotificationBarrage.sln --configfile .\NuGet.Config
dotnet test .\NotificationBarrage.sln --no-restore
dotnet run --project .\src\NotificationBarrage -- --demo
```

未打包启动可以测试界面；**接收通知需要安装具有包身份及通知访问声明的 MSIX**。权限由用户在设置窗口点击「连接通知」授予，后台不会反复弹权限框。

本机普通 `dotnet` 只有运行时；Rider SDK 位于：

```powershell
$sdk = 'D:\JetBrains Rider 2024.1.5\lib\ReSharperHost\windows-x64\dotnet\dotnet.exe'
```

离线检查不依赖 NuGet，使用 SDK 编译器和本地参考程序集编译真实领域/服务源码，再执行控制台断言（**不是 xUnit 执行结果**）：

```powershell
.\scripts\Invoke-OfflineChecks.ps1 -DotnetPath $sdk
.\scripts\Build-Preview.ps1 -DotnetPath $sdk
.\artifacts\preview\NotificationBarrage.Preview.exe --smoke --capture
```

`--smoke` 使用内置测试消息，约 12 秒后退出；`--capture` 输出本应用界面预览和覆盖层诊断，不截取其他窗口或读取消息。普通启动不生成诊断文件。

## 完整版 MSIX

安装 Windows 10/11 SDK（含 MakeAppx 和 SignTool），网络恢复后执行：

```powershell
.\scripts\Build-Msix.ps1 -DotnetPath $sdk
```

脚本发布 x64 自包含应用并生成 `artifacts/msix/NotificationBarrage-1.0.0.0-x64.msix`。默认是未签名包，安装前必须签名。可通过 `-CertificatePath` / `-CertificatePassword` 提供代码签名证书，证书 Subject 必须匹配清单 `CN=NotificationBarrage.Dev`，或同时修改清单 Publisher。脚本不会自动创建/信任证书、安装软件或启用开机启动。

本地开发可使用 Visual Studio 的 MSIX 签名工具生成测试证书，再按 Windows 提示检查并信任开发证书。正式分发使用代码签名证书或 Microsoft Store 流程。

安装签名包后：

1. 从开始菜单打开「NotifyBar」。
2. 点击「连接通知」，允许 Windows 通知访问；被拒绝时通过「通知访问设置」重新开启。
3. 检查 QQ、微信的消息通知和内容预览，确认新消息进入 Windows 通知中心。
4. 在窗口化/无边框游戏中发送测试弹幕，再分别接收真实 QQ 和微信消息。
5. 可选择「登录 Windows 后启动」。实际开机启动通过 MSIX StartupTask 管理；Windows 拒绝时会显示原因。

## 文件结构

| 路径 | 作用 |
|---|---|
| `src/NotificationBarrage/Domain` | 设置、消息、通知快照、屏幕布局等纯逻辑 |
| `src/NotificationBarrage/Services` | WinRT 适配、来源筛选、队列、保存与日志 |
| `src/NotificationBarrage/UI` | WPF 设置窗口、弹幕条、透明覆盖层 |
| `src/NotificationBarrage/AppHost.cs` | 托盘、权限入口、暂停、生命周期及服务连接 |
| `packaging` | MSIX 清单与图标 |
| `scripts` | 离线预览、检查、完整打包 |
| `tests` | xUnit 测试源码及离线检查入口 |
| `docs/verification.md` | 已验证结果、限制、后续验收项 |

配置位于 `%AppData%\NotificationBarrage\settings.json`。损坏配置会备份并恢复默认值；保存失败会提示且不宣称成功。日志位于同目录 `logs` 子目录，单文件不超过 2 MB。

卸载：先从托盘退出，再在 Windows「已安装的应用」中卸载。Git 提交信息统一使用中文；执行 `git log -1 --oneline` 查看本地最近提交。
