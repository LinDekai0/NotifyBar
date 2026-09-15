# NotifyBar 通用通知来源实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox syntax.

**Goal:** 用户选择各 Windows 通知应用，交付可编译安装的 NotifyBar，并验证系统通知链路。

**Architecture:** WindowsNotificationSource 发现快照中的来源，来源目录和配置以 AppUserModelId 关联，AppHost 把已启用的新通知送入现有队列/覆盖层。来源选择 UI 动态更新并保护未保存修改。

**Tech Stack:** .NET 8 WPF、Windows UserNotificationListener、System.Text.Json、xUnit、PowerShell、MSIX。

**Spec:** docs/superpowers/specs/2026-09-15-all-notification-sources-design.md

## Global Constraints

- 只处理 Windows 通知中心；不读取聊天窗口、不注入游戏、不上传正文、不发送外部消息。
- NotifyBar 用户名称，内部 NotificationBarrage 标识保持。中文 UI/提交说明。
- 新来源默认关闭，旧 QQ/微信配置迁移；按精确应用 ID 选择和去重。
- 启动/恢复发现来源但不回放历史；3 条/秒、5 条并发、暂停 10 条、排队 100 条。
- 真实通知正文不写日志、验证报告或截图；仅应用界面可截取。

### Task 1: 通用来源选择、发现和界面

**Files:** Domain/AppSettings.cs、新增来源模型/目录、Services/SettingsStore.cs、MessageFilter.cs、WindowsNotificationSource.cs、INotificationSource.cs、AppHost.cs、Domain/BarrageMessage.cs、UI/SettingsWindow.cs、BarrageItemControl.cs、tests、Invoke-OfflineChecks.ps1、README.md（功能说明）。

**Interfaces:** IncomingNotification 已含 AppId/AppName；BarrageMessage 增加 AppId 用于队列显示前筛选。来源目录向 UI 提供 ID/显示名快照；配置向过滤器提供统一 IsSourceEnabled 语义；首次快照先发现再交给 NotificationSnapshotTracker。

- [ ] 先添加真实行为失败测试：Telegram ID 启用后通过、同名不同 ID 不通过、新来源关闭、旧 QQ/微信 JSON 迁移、空选择保存/读取、重复来源/名称变化、首次快照不回放、禁用积压。
- [ ] 实现来源目录和选择持久化，保留旧配置兼容；本地文件只含来源元数据和选择。
- [ ] 替换二元筛选，正文去重包含 AppId，显示前按 AppId 重查。
- [ ] 动态设置列表、搜索/全选/清空/刷新、无来源说明，刷新和异步保存保留用户选择；覆盖层支持通用名称。
- [ ] 执行离线检查、预览构建；恢复依赖后运行 xUnit 和完整 Release 构建。独立审查后以中文提交。

### Task 2: 完整版构建、安装和接收验收

**Files:** scripts/Build-Msix.ps1、Restore-Dependencies.ps1、按需新增开发安装和系统 Toast 验证脚本、packaging/Package.appxmanifest、README.md、docs/verification.md。

**Interfaces:** Task 1 交付可接收任意所选来源的完整 App；Build-Msix 产出实际发布文件、清单、有效签名包；安装流程仅安装本项目包。

- [ ] 验证 SDK/MakeAppx/SignTool/用户上下文；恢复官方 NuGet。修复编译阻断并明确日志。
- [ ] 运行 `dotnet test NotificationBarrage.sln -c Release`、预览/完整程序烟雾测试。
- [ ] 发布自包含 x64 包；签名与证书只针对当前应用，提供可复用安装命令，保留用户证书许可边界。
- [ ] 安装包，打开设置，验证权限与系统 Toast 接收（仅非敏感自生成文本）；用户真实通信软件消息无法自动触发则提供验收步骤。
- [ ] 更新文档事实、产物链接和阻塞项；独立审查；中文提交并按已有授权同步 GitHub。

## Self-review

Task 1 覆盖来源身份、迁移、发现、UI 和核心行为。Task 2 覆盖完整版依赖、MSIX 和运行验收；系统权限/真实外部消息须以事实报告。未依赖未定义的跨任务技术签名；Task 2 只消费最终可构建项目及已有脚本。
