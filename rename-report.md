# NotifyBar 改名报告

## 改动文件

- `README.md`：标题、移动说明和开始菜单入口改为 NotifyBar。
- `src/NotificationBarrage/AppHost.cs`：托盘名称、状态文字、气泡提示改为 NotifyBar。
- `src/NotificationBarrage/App.xaml.cs`：启动失败和运行错误提示改为 NotifyBar。
- `src/NotificationBarrage/UI/SettingsWindow.cs`：设置窗口标题与主标题改为 NotifyBar。
- `src/NotificationBarrage/UI/OverlayWindow.cs`：覆盖层窗口标题改为 NotifyBar。
- `src/NotificationBarrage/Services/StartupRegistration.cs`：开机启动错误提示改为 NotifyBar。
- `packaging/Package.appxmanifest`：应用显示名、视觉元素显示名和 StartupTask 显示名改为 NotifyBar。

内部程序集、命名空间、项目文件名、配置目录、互斥体和设计文档标题均保持 `NotificationBarrage` 或原样。

## 验证

执行命令：

```powershell
rg -n --hidden -S "通知弹幕" README.md src packaging -g '!artifacts' -g '!.git'
```

结果：无匹配。随后检查 `NotifyBar` 用户可见文案及内部 `NotificationBarrage` 标识仍存在。未执行 Git 提交。
