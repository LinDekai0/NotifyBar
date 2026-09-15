# 2026-09-15 验证记录

## 已验证

- 使用本机 Rider 内置 .NET SDK 8.0.101、本机 .NET 8 参考程序集，直接编译预览版：零警告、零错误。
- 实际运行 WPF 预览 EXE，测试消息入队、显示，自动退出码 0。
- 原生窗口样式：Topmost=true、ShowActivated=false、ClickThrough=true、NoActivate=true、ToolWindow=true。主屏幕布局 2048×1152 DIP，5 个通道。
- 离线控制台 34 项检查通过，直接编译并调用生产领域/服务源码：来源精确匹配、禁用来源、空内容、Unicode 截断、去重、速率/并发限制、暂停保留、队列上限、旧通知基线、恢复不重放、布局、JSON 损坏恢复、日志上限与隐私。
- PowerShell 脚本语法检查通过。
- Windows SDK 10.0.26100 的 MakeAppx 使用受控的预览主机测试载荷，校验生产 MSIX 清单及图标成功。临时包仅用于清单校验，保存在忽略的工作记录目录，不可安装或当作完整版。

## 未验证及阻塞

- 常规 dotnet restore/test/publish 尚未完成：最新恢复命令在读取 `C:\Users\lon08\AppData\Local\Microsoft SDKs` 时被拒绝，尚未进入下载；提升权限被自动审批服务以 429 Too Many Requests 拒绝。日志位于 `artifacts/setup-logs/restore.log`。
- 全量 Windows WinRT 源码未通过常规编译验证，xUnit 未执行。离线预览使用 PREVIEW 分支，不包含真实 WinRT 或开机启动接入。
- 未生成可安装的完整 MSIX，未签名、信任证书或安装。
- 未读取或测试用户的真实 QQ/微信通知，也未在实际游戏、不同 DPI/显示器布局、权限撤销或开机启动中完成集成验收。
- 已有独立 Git 仓库和 `codex/notification-barrage` 分支，本地提交以 `git log -1 --oneline` 为准。原网页 index.html/styles.css 修改仍保留。
- 用户已授权移动到 `D:\notification-barrage`。源目录与目标不存在的检查通过，但移动命令被自动审批服务 429 拒绝，项目仍在 `D:\股价信息预测系统\notification-barrage`。可在用户 PowerShell 执行 `scripts/Move-ToDriveRoot.ps1`。
- 本机运行中的微信为 `4.1.13.65`、QQ 为 `9.9.21.39038-6a73892f`。代理的通知注册诊断在沙箱账户下运行，无法据此判断用户账户的微信通知支持情况；须实测新消息是否出现在通知中心。

## 执行方式

任务 1、2 通过实现子代理与独立审查/复审完成。后半部分曾因子代理工具不可用，由主代理实现和本地复核。续接后独立最终审查发现关闭开机启动误报成功及缺少全局异常处理两项问题，修复子代理已处理；主代理核对了修改范围。修复后尚无独立子代理复审结论。

最新修复后：预览编译成功，34 项离线行为检查通过。开机启动禁用后会重新读取系统状态，失败不会报告保存成功。同步启动、UI 和未观察任务异常现有脱敏记录及错误提示；未知异常会进入退出流程。真实 StartupTask 和异常 UI 仍待集成验收。此前烟雾测试属于修复前版本，修复后启动 GUI 的请求被审批服务拒绝，不能将旧烟雾测试结果视为最新代码的运行证据。

## 实现裁决与代价

1. 在指定新目录开发，Git 分支受限时保留文件；代价是尚未形成 Git 提交。
2. .NET 8 使用 Windows TFM 的 WinRT 投影，移除不兼容的旧 Contracts 包及未使用 DI；最低运行版本仍为 Windows 10 1903。代价是完整构建需恢复 Windows 引用包。
3. 更正事件枚举为 UserNotificationChangedKind.Added、权限清单为 uap3:userNotificationListener；开机启动使用 StartupTask。代价是需要包身份及 Windows 用户许可。
4. 普通置顶窗口不注入游戏；代价是不能保证覆盖独占全屏。
5. 待显示队列上限 100，滚动时间去重；代价是通知风暴会丢弃较旧积压。
6. 增加显式预览构建和离线检查；代价是预览不能接收真实通知，其结果不等价于完整版验收。
7. WPF 窗口使用 UI 目录中的 C# 控件代码，避免引入 UI 框架；代价是没有可视 XAML 设计器布局。
8. Windows SDK MakeAppx 脚本替代依赖 Visual Studio 特定组件的 wapproj；代价是签名证书需由开发者提供。

## 后续验收

- [ ] 恢复 NuGet、常规 Release 构建及 xUnit 通过。
- [ ] 生成并签名安装完整 MSIX。
- [ ] 授权后 QQ/微信新通知各显示一次，禁用来源生效。
- [ ] 暂停/恢复、撤销后重新授权、开机启动正常。
- [ ] 实际无边框游戏中可见、可点穿、不抢焦点。
- Git 提交使用中文描述，可用 `git log -1 --oneline` 核对结果。

最后一次预览动画验收：QueueActive=0、OverlayActive=0、Pending=0，SMOKE_EXIT=0。界面已通过 WPF 自身渲染输出检查，保存/测试按钮固定在窗口底部。
