# NotifyBar

Windows 桌面应用，把用户选中的应用发到 **Windows 通知中心** 的新通知显示成游戏画面上的滚动弹幕。

## 安装与首次使用

完整版为 **1.1.1.0 / Windows x64 / 自包含 .NET 8.0.31**，安装后无需另装 .NET。
本机安装包位于 artifacts/msix/NotificationBarrage-1.1.1.0-x64.msix，公钥证书为同目录 NotifyBar-Dev.cer。构建产物和证书不提交到 Git。

在 **Windows PowerShell 5.1** 中执行：

~~~powershell
Set-Location 'C:\path\to\NotifyBar'
.\scripts\Install-DevMsix.ps1 -TrustCertificate
~~~

首次安装会请求 Windows 管理员确认，只将此项目证书添加到 LocalMachine\TrustedPeople。包签名和证书一致性通过检查后，应用安装到原登录用户；不会添加根证书、关闭签名验证或启用开机启动。取消确认后可重新运行。

1. 从开始菜单打开 **NotifyBar**。若未显示设置窗口，双击右下角托盘图标（可能收起在上箭头菜单）。点击 **连接通知** 并在 Windows 提示中允许访问；已连接时可直接选择来源。
2. 让目标软件产生一条 Windows 通知；Windows 11 按 Win + N、Windows 10 按 Win + A 检查它是否在通知中心。
3. 回到 NotifyBar，刷新来源，勾选该软件并保存。新安装及新发现的来源默认关闭。
4. 等待该软件的下一条新通知。首次启动和权限恢复时已有的通知不回放。
5. 先用“发送测试弹幕”检查游戏覆盖效果；关闭设置窗口后应用继续在托盘运行，右键可暂停、恢复或退出。

如果管理员确认没有出现，在普通 Windows PowerShell 中重新执行安装命令即可。系统通知访问权限仍由本人在 Windows 中授予。

## 验证 Windows 通知链路

应用内的“发送测试弹幕”只测试显示效果。下面脚本会通过 **Windows PowerShell** 的注册来源发出一条真正的本机 Windows Toast，不联系任何聊天服务：

~~~powershell
powershell.exe -NoProfile -File '.\scripts\Send-TestNotification.ps1'
~~~

第一次运行后，在 NotifyBar 中刷新来源，勾选 **Windows PowerShell** 并保存，再运行一次命令。应看到含本次测试标记的通知弹幕。脚本的 PresentInSenderHistory=true 只证明通知已进入该来源的系统历史；实际弹幕仍需检查。两次测试正文包含不同标记，以免短时间去重隐藏第二次测试。测试完成后可取消勾选 Windows PowerShell 并保存。

Telegram Desktop、Microsoft Teams、Outlook 等可作为候选；客户端版本、通知模式和系统设置会影响它们是否使用 Windows 通知。**以目标电脑实际出现的 Windows 通知为准**。微信自有弹窗不接入，也不读取聊天窗口。

真实外部来信验收：开启目标软件的 Windows 系统通知及正文预览，接收一条正常来信以发现来源，勾选保存后再接收一条新来信。确认只显示一次；关闭该来源并保存，再次来信应不显示。不要将本机测试 Toast 等同于外部通讯软件验收。

## 性能与隐私

### 卡顿修复

弹幕覆盖层现在只占用弹幕带高度，不再覆盖整块屏幕；移动内容使用位图缓存，不使用每帧重算的动态阴影，也不再每两秒调用置顶 API。这样可以避免周期性合成停顿，并降低长中文、表情和 QQ 多段通知的重绘开销。窗口仍保持置顶、鼠标穿透和不抢焦点。性能和验证细节见 docs/performance.md。

### 隐私检查

NotifyBar 只读取 Windows 通知中心中你主动选择的应用，不读取聊天窗口、不发送或回复消息、不联网上传、不把通知标题或正文写入日志和配置。GitHub 仓库仅包含源代码、测试、脚本和文档；.gitignore 排除了 artifacts/、.tools/、.superpowers/、bin/、obj/、证书和日志。提交前已检查 Git 跟踪文件，没有本机用户名路径、通知正文、私钥、PFX/CER 或运行时验证产物。

## 功能与边界

- 来源列表来自已发现的通知，**不是全部已安装软件清单**。支持搜索、逐项选择、全选、清空、刷新；刷新保留未保存勾选。
- 来源身份使用精确 AppUserModelId；同名不同应用不会互相放行。旧 QQ/微信选择仅在可识别应用 ID 出现后迁移，不能确认 ID 的来源由用户勾选。
- 可显示应用名、标题和正文；没有正文预览时无法还原聊天内容。
- 每秒最多接收 3 条、最多同时显示 5 条；等待队列上限 100，暂停时保留最近 10 条。
- 主屏幕透明置顶、不抢焦点、鼠标穿透；位置、速度、字体、透明度和长度可调。
- 适用于窗口化 / 无边框全屏游戏；不能保证覆盖独占全屏。不注入游戏进程。
- 设置和日志只保存在本机，不上传、不回复消息。来源目录仅保存 ID、名称和选择；通知标题、正文不写入配置或日志。
- 勿扰/专注模式及客户端通知策略可能影响实际通知产生，需实测。
- 配置目录沿用 %AppData%\NotificationBarrage。安装版可能经 Windows 包数据重定向保存到 %LocalAppData%\Packages\<包家族名>\LocalCache\Roaming\NotificationBarrage。内部程序集名称保持 NotificationBarrage。
- 卸载前从托盘退出，再到 Windows“已安装的应用”卸载。开发证书如需移除，只移除此项目证书，勿清空证书库。

## 开发与编译

环境：Windows 10 1903+ / Windows 11 x64、.NET 8 SDK；MSIX 打包另需 Windows 10/11 SDK 的 MakeAppx 和 SignTool。

~~~powershell
Set-Location 'C:\path\to\NotifyBar'
.\scripts\Restore-Dependencies.ps1
~~~

恢复脚本查找 .NET 8 SDK，使用官方 NuGet 和项目内 .tools/packages 缓存，执行 Release 构建及 xUnit。失败日志位于 artifacts/setup-logs。

本机 Rider SDK 可显式指定；其他电脑可使用自己的 SDK 路径：

~~~powershell
$sdk = (Get-Command dotnet.exe).Source
.\scripts\Restore-Dependencies.ps1 -DotnetPath $sdk
.\scripts\Invoke-OfflineChecks.ps1 -DotnetPath $sdk
.\scripts\Build-Preview.ps1 -DotnetPath $sdk
~~~

离线检查直接编译生产领域/服务代码，独立于 xUnit。预览目录为 artifacts/preview，预览不接收系统通知，运行需 .NET 8 Desktop Runtime。

生成自包含完整版：

~~~powershell
.\scripts\Build-Msix.ps1 -DotnetPath $sdk -CertificateThumbprint '你自己的开发证书指纹'
~~~

证书需在 CurrentUser\My 且带私钥，Subject 必须匹配清单的 CN=NotificationBarrage.Dev。若本机没有，可在 Windows PowerShell 中为自己创建：

~~~powershell
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=NotificationBarrage.Dev' -FriendlyName 'NotifyBar Local Development' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(1)
New-Item -ItemType Directory -Force '.\artifacts\msix' | Out-Null
Export-Certificate -Cert $cert -FilePath '.\artifacts\msix\NotifyBar-Dev.cer' | Out-Null
.\scripts\Build-Msix.ps1 -DotnetPath $sdk -CertificateThumbprint $cert.Thumbprint
~~~

只导出公钥，不提交私钥。脚本也兼容已有 PFX 的 CertificatePath / CertificatePassword 参数。不传证书时生成未签名包，不能安装。正式分发需合适的代码签名或 Microsoft Store 流程。

Build-Msix 默认固定 .NET 8.0.31，可通过 RuntimeVersion 调整补丁版；只覆盖 NETCore/WindowsDesktop 框架的版本，避免旧 SDK 错误地请求不存在的 Windows SDK 引用包。发布目录、运行时版本和包 SHA256 保存在 artifacts/msix/build-info.json。

## 验证状态与结构

本机已完成完整版编译、签名 MSIX 安装和 Windows PowerShell 系统 Toast 接收/显示验证；27 项 xUnit 与 42 项离线检查通过。弹幕覆盖层采用弹幕带大小窗口、位图缓存，并移除周期性置顶刷新，减少周期性卡顿。外部通讯软件来信和实际游戏仍需在目标环境验收。详细结果见 [docs/verification.md](docs/verification.md)。区分构建、显示测试、安装、通知授权、本机 Toast 和外部软件真实来信，未验收项目不会按成功报告。

| 路径 | 作用 |
|---|---|
| src/NotificationBarrage/Domain | 来源目录、设置、消息、通知快照与布局 |
| src/NotificationBarrage/Services | WinRT 适配、筛选、队列、保存与日志 |
| src/NotificationBarrage/UI | 设置窗口、弹幕条、透明覆盖层 |
| src/NotificationBarrage/AppHost.cs | 托盘、权限、暂停、生命周期 |
| packaging | MSIX 清单与图标 |
| scripts | 编译、打包、开发安装、本机 Toast |
| tests | xUnit 与离线行为检查 |

Git 提交说明使用中文。仓库：https://github.com/LinDekai0/NotifyBar
