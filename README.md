# NotifyBar

NotifyBar 是 Windows 桌面弹幕工具：它只读取你选择的 **Windows 通知中心** 来源，把新通知显示在游戏画面上方。

## 快速开始

### 1. 下载并安装

从 GitHub Releases 下载最新的 `NotificationBarrage-<版本>-x64.msix`。

开发测试包需要先信任随包提供的公钥证书，然后在 Windows PowerShell 5.1 执行：

```powershell
Set-Location '你的 NotifyBar 文件夹'
.\scripts\Install-DevMsix.ps1 -TrustCertificate
```

从开始菜单打开 **NotifyBar**。

> 完整通知接收必须使用 MSIX。Windows 的 `UserNotificationListener` 需要应用包身份和通知访问声明。预览 EXE 只用于显示效果测试，不能读取真实通知。

### 2. 允许读取通知

1. 打开 NotifyBar 设置，点击“连接通知”。
2. 在 Windows 提示中允许通知访问。
3. 让目标软件产生一条系统通知。
4. 回到 NotifyBar，点击“刷新”。
5. 勾选目标应用，点击“保存设置”。
6. 接收下一条新通知。

首次启动、重新授权或恢复权限时，通知中心里已有的旧消息不会回放。新发现的来源默认关闭，必须由你主动勾选。

### 3. 测试系统通知

下面命令只发送固定的本机测试文本，不联系 QQ、微信或其他聊天服务：

```powershell
powershell.exe -NoProfile -File '.\scripts\Send-TestNotification.ps1'
```

第一次运行后刷新来源，勾选“Windows PowerShell”并保存，再运行一次。看到测试文本弹过，说明 Windows 通知接收链路正常。

## 支持范围

NotifyBar 可以读取任何真正写入 Windows 通知中心的应用，例如 Telegram Desktop、Microsoft Teams、Outlook，以及部分版本的 QQ。是否支持取决于客户端版本和系统通知设置。

微信自有弹窗不属于 Windows 通知中心，因此不在读取范围内。NotifyBar 不读取聊天窗口，不回复或发送消息，不注入游戏进程，也不上传通知内容。

应用必须提供通知正文预览，NotifyBar 才能显示正文。Windows 勿扰模式、专注助手、游戏模式和应用自身的通知设置可能阻止通知产生。

## 弹幕效果

- 透明置顶、鼠标穿透、不抢焦点。
- 支持顶部、中部、底部、速度、字体、透明度和正文长度调整。
- 最多同时显示 5 条，队列最多保留 100 条。
- 适合窗口化和无边框全屏游戏；独占全屏不保证覆盖。
- 覆盖层只占用弹幕带高度，移动内容使用位图缓存，并避免周期性置顶刷新，以减少卡顿。

## 隐私

NotifyBar 只在本机读取 Windows 通知中心，并按你选择的 AppUserModelId 过滤。设置文件只保存来源 ID、显示名称和开关；通知标题和正文不会写入配置或日志。日志只记录错误类型和运行状态，不记录聊天内容。

仓库不包含本机配置、通知历史、用户名路径、证书私钥、PFX/CER、构建产物或日志；这些路径已在 `.gitignore` 中排除。提交前会检查 Git 跟踪文件，避免把本地隐私推送到 GitHub。

## 从源码构建

环境：Windows 10 1903 或 Windows 11、x64、.NET 8 SDK。生成 MSIX 还需要 Windows SDK 的 MakeAppx 和 SignTool。

```powershell
Set-Location '你的 NotifyBar 文件夹'
$sdk = (Get-Command dotnet.exe).Source

.\scripts\Restore-Dependencies.ps1 -DotnetPath $sdk
.\scripts\Invoke-OfflineChecks.ps1 -DotnetPath $sdk
.\scripts\Build-Preview.ps1 -DotnetPath $sdk
```

预览版输出到 `artifacts\preview`，运行：

```powershell
.\artifacts\preview\NotificationBarrage.Preview.exe --demo
```

预览版只测试弹幕显示。生成可安装的自包含 MSIX：

```powershell
.\scripts\Build-Msix.ps1 -DotnetPath $sdk -CertificateThumbprint '你的代码签名证书指纹'
```

安装包输出到 `artifacts\msix`。正式分发请使用受信任的代码签名证书或 Microsoft Store。

## 项目结构

| 路径 | 用途 |
|---|---|
| `src/NotificationBarrage/Domain` | 设置、通知来源、消息和布局 |
| `src/NotificationBarrage/Services` | Windows 通知监听、筛选、队列、日志 |
| `src/NotificationBarrage/UI` | 设置窗口和透明弹幕覆盖层 |
| `packaging` | MSIX 清单和图标 |
| `scripts` | 恢复依赖、检查、预览、打包和本机通知测试 |
| `tests` | xUnit 和离线行为检查 |

详细验证记录见 [docs/verification.md](docs/verification.md)，性能定位见 [docs/performance.md](docs/performance.md)。
