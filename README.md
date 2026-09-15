# NotifyBar

NotifyBar 是一个 Windows 桌面通知弹幕工具。它会把你选择的 Windows 通知中心消息，显示成醒目的滚动弹幕。

做文档、看视频，甚至打游戏时，你都能第一时间看到通知：朋友的娱乐邀请、家人的消息或上司的工作安排，都不容易被错过。

## 快速安装

支持 Windows 10 1903 及以上版本、Windows 11，仅支持 x64。安装包已包含 .NET 运行时，无需另外安装。

### 第一步：下载文件

在 GitHub Releases 下载以下两个文件：

- `NotifyBar-Dev.cer`
- `NotificationBarrage-1.1.3.0-x64.msix`

### 第二步：信任证书

本版本使用自签名开发证书，第一次安装需要信任证书。证书只用于确认安装包来自本项目：

1. 双击 `NotifyBar-Dev.cer`，点击“安装证书”。
2. 选择“本地计算机”，点击“下一步”，并允许管理员确认。
3. 选择“将所有的证书放入下列存储”，点击“浏览”。
4. 选择“受信任人（Trusted People）”，确认并完成导入。

### 第三步：安装 NotifyBar

双击 `NotificationBarrage-1.1.3.0-x64.msix`，点击“安装”。安装完成后，从开始菜单打开 **NotifyBar**。

> 完整通知接收必须使用 MSIX。预览版 EXE 只能测试弹幕显示，不能读取 Windows 通知中心。

## 首次使用

1. 打开 NotifyBar 设置，点击“连接通知”，并在 Windows 提示中允许通知访问。
2. 让目标应用产生一条 Windows 系统通知。
3. 回到 NotifyBar，点击“刷新”。
4. 点击目标应用旁边的勾选框，然后点击“保存设置”。
5. 等待下一条新通知，或点击“发送测试弹幕”检查显示效果。

已有的历史通知不会回放。新发现的通知来源默认关闭，需要手动勾选。

## 支持哪些应用

NotifyBar 可以读取真正写入 Windows 通知中心的应用，例如 Telegram Desktop、Microsoft Teams、Outlook，以及部分版本的 QQ。是否支持取决于应用版本和 Windows 通知设置。

微信自有弹窗不属于 Windows 通知中心，因此不在读取范围内。NotifyBar 不读取聊天窗口，不发送或回复消息，也不注入其他程序。

如果应用没有把通知写入 Windows 通知中心，或关闭了通知正文预览，NotifyBar 就无法显示对应内容。

## 弹幕设置

- 可调整弹幕位置、速度、字体大小、透明度和正文长度。
- 支持置顶、鼠标穿透和不抢焦点。
- 最多同时显示 5 条消息，等待队列最多保留 100 条。
- 可用于办公、学习、看视频和游戏等场景。

## 隐私说明

NotifyBar 只在本机读取 Windows 通知中心，并按你选择的应用过滤。通知标题和正文不会写入配置或日志，不会上传到网络，也不会发送给任何人。

仓库不包含本机配置、通知历史、用户名路径、证书私钥、PFX/CER、构建产物或日志。

## 常见问题

**没有看到某个应用？**

先让该应用产生一条 Windows 系统通知，再回到 NotifyBar 点击“刷新”。来源列表只显示已经被 Windows 发现的通知应用。

**安装时提示证书不受信任？**

请先完成上面的证书导入步骤，并确认导入位置是“受信任人（Trusted People）”，不是“受信任的根证书颁发机构”。

**通知中心里没有微信？**

NotifyBar 只读取 Windows 通知中心。微信自有弹窗没有进入通知中心时，无法被读取。

**想卸载怎么办？**

先退出 NotifyBar，再到 Windows“设置 → 应用 → 已安装的应用”中卸载。开发证书如需移除，只删除本项目的 `CN=NotificationBarrage.Dev` 证书。

## 从源码构建

需要 Windows 10/11、x64、.NET 8 SDK；生成 MSIX 还需要 Windows SDK 的 MakeAppx 和 SignTool。

```powershell
Set-Location '你的 NotifyBar 文件夹'
$sdk = (Get-Command dotnet.exe).Source

.\scripts\Restore-Dependencies.ps1 -DotnetPath $sdk
.\scripts\Invoke-OfflineChecks.ps1 -DotnetPath $sdk
.\scripts\Build-Preview.ps1 -DotnetPath $sdk
```

预览版输出到 `artifacts\\preview`，只用于测试显示效果。完整通知接收版本需要使用 `Build-Msix.ps1` 生成 MSIX。

性能说明见 [docs/performance.md](docs/performance.md)。
