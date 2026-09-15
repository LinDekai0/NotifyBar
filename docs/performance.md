# 卡顿定位结果
- 现象：弹幕移动约每两秒卡顿一次。
- 根因：OverlayWindow 使用每 2 秒一次的 DispatcherTimer 调用 SetWindowPos 维持置顶，触发透明窗口重新合成。
- 修复：移除周期性 SetWindowPos；保留 Topmost=true，并将窗口限制为弹幕带高度。
- 长中文/表情会增加 WPF 文本测量成本，因此每条弹幕使用 BitmapCache；移除动态 DropShadowEffect。
- 隐私：仅检查 Git 跟踪文件和公开文档；通知正文不会写入配置、日志或仓库，构建产物、证书和本机验证 JSON 均被 .gitignore 排除。
