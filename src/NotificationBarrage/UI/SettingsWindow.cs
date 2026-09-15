using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NotificationBarrage.Domain;

namespace NotificationBarrage.UI;

public sealed class SettingsWindow : Window
{
    private readonly CheckBox _qq = new() { Content = "QQ", Margin = new Thickness(0, 8, 30, 8) };
    private readonly CheckBox _wechat = new() { Content = "微信", Margin = new Thickness(0, 8, 30, 8) };
    private readonly CheckBox _startup = new() { Content = "登录 Windows 后启动", Margin = new Thickness(0, 14, 0, 14) };
    private readonly ComboBox _position = new() { ItemsSource = new[] { "顶部", "中部", "底部" }, Width = 160, HorizontalAlignment = HorizontalAlignment.Left, Foreground = Brushes.Black, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _status = Text("正在检查通知连接…", 14, "#A5B4C8");
    private readonly TextBlock _result = Text("设置保存在本机；聊天内容不写入日志。", 12, "#94A3B8");
    private readonly Slider _speed, _font, _opacity, _length, _offset;
    private readonly Button _save;
    private readonly Func<AppSettings, Task<string?>> _saveSettings;

    public SettingsWindow(AppSettings settings, Func<AppSettings, Task<string?>> saveSettings, Action test,
        Func<Task> connect, Action openAccess, Action openBanners)
    {
        _saveSettings = saveSettings;
        Title = "通知弹幕 · 设置";
        Width = Math.Min(790, SystemParameters.WorkArea.Width - 30);
        Height = Math.Min(830, SystemParameters.WorkArea.Height - 30); MinWidth = 540; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0B1120"); Foreground = Brush("#E2E8F0");
        _qq.Foreground = _wechat.Foreground = _startup.Foreground = Foreground;
        FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 14;
        var root = new StackPanel { Margin = new Thickness(30, 24, 30, 24) };
        root.Children.Add(Text("通知弹幕", 29, "#F8FAFC", true));
        root.Children.Add(Text("专心游戏，也不错过一句重要的话", 13, "#94A3B8"));
        var preview = new StackPanel();
        preview.Children.Add(Text("消息出现时", 11, "#94A3B8"));
        preview.Children.Add(Text("微信   小伙伴：等你这局结束，一起开黑！", 19, "#6EE7B7", true));
        root.Children.Add(Card(preview));

        var connection = new StackPanel();
        connection.Children.Add(Text("通知连接", 16, "#F8FAFC", true));
        _status.Margin = new Thickness(0, 8, 0, 12); connection.Children.Add(_status);
        var actions = new WrapPanel();
        var connectButton = Button("连接通知", "#0E7490");
        connectButton.Click += async (_, _) => { connectButton.IsEnabled = false; try { await connect(); } finally { connectButton.IsEnabled = true; } };
        actions.Children.Add(connectButton);
        var access = Button("通知访问设置"); access.Click += (_, _) => openAccess(); actions.Children.Add(access);
        var banners = Button("QQ / 微信通知设置"); banners.Click += (_, _) => openBanners(); actions.Children.Add(banners);
        connection.Children.Add(actions); root.Children.Add(Card(connection));

        var options = new StackPanel();
        options.Children.Add(Text("显示偏好", 16, "#F8FAFC", true));
        var sources = new StackPanel { Orientation = Orientation.Horizontal }; sources.Children.Add(_qq); sources.Children.Add(_wechat); options.Children.Add(sources);
        options.Children.Add(Text("弹幕区域", 12, "#94A3B8")); options.Children.Add(_position);
        _offset = AddSlider(options, "垂直偏移", -500, 500, settings.VerticalOffset, "px");
        _speed = AddSlider(options, "滚动速度", 50, 2000, settings.SpeedPixelsPerSecond, "px/s");
        _font = AddSlider(options, "文字大小", 12, 72, settings.FontSize, "px");
        _opacity = AddSlider(options, "不透明度", 20, 100, settings.Opacity * 100, "%");
        _length = AddSlider(options, "正文最大长度", 20, 500, settings.MaxBodyLength, "字");
        options.Children.Add(_startup);
        options.Children.Add(Text("适用于窗口化 / 无边框全屏游戏。独占全屏可能无法显示，请先发送测试弹幕。", 12, "#94A3B8"));
        root.Children.Add(Card(options));
        var footer = new WrapPanel();
        _save = Button("保存设置", "#0E7490"); _save.Click += async (_, _) => await SaveAsync(); footer.Children.Add(_save);
        var testButton = Button("发送测试弹幕"); testButton.Click += (_, _) => test(); footer.Children.Add(testButton);
        var fixedFooter = new StackPanel { Margin = new Thickness(30, 8, 30, 16) };
        fixedFooter.Children.Add(footer); _result.Margin = new Thickness(0, 10, 0, 0); fixedFooter.Children.Add(_result);
        var layout = new DockPanel { Background = Background };
        DockPanel.SetDock(fixedFooter, Dock.Bottom); layout.Children.Add(fixedFooter);
        layout.Children.Add(new ScrollViewer { Content = root, Background = Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = layout;
        _qq.IsChecked = settings.EnableQQ; _wechat.IsChecked = settings.EnableWeChat;
        _position.SelectedIndex = (int)settings.Position; _startup.IsChecked = settings.StartWithWindows;
    }

    public void SetStatus(ListenerStatus status)
    {
        _status.Text = status.Message;
        _startup.IsEnabled = status.Access != NotificationAccessStatus.NeedsPackage;
        _startup.ToolTip = _startup.IsEnabled ? "由 Windows 的启动应用设置管理" : "安装完整版 MSIX 后可使用开机启动";
    }
    public void ShowResult(string message) => _result.Text = message;
    public void CapturePreview(string path)
    {
        UpdateLayout();
        var visual = (FrameworkElement)Content;
        var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private async Task SaveAsync()
    {
        _save.IsEnabled = false;
        try
        {
            var settings = new AppSettings
            {
                EnableQQ = _qq.IsChecked == true, EnableWeChat = _wechat.IsChecked == true,
                Position = (BarragePosition)_position.SelectedIndex, VerticalOffset = (int)_offset.Value,
                SpeedPixelsPerSecond = (int)_speed.Value, FontSize = _font.Value,
                Opacity = _opacity.Value / 100, MaxBodyLength = (int)_length.Value, StartWithWindows = _startup.IsChecked == true
            }.Normalize();
            _result.Text = await _saveSettings(settings) ?? "已保存。新弹幕将使用这些设置。";
        }
        catch { _result.Text = "保存失败，请检查本机设置目录是否可写。"; }
        finally { _save.IsEnabled = true; }
    }

    private static Slider AddSlider(Panel target, string name, double min, double max, double value, string unit)
    {
        var label = Text($"{name}    {value:0} {unit}", 13, "#CBD5E1"); label.Margin = new Thickness(0, 10, 0, 3); target.Children.Add(label);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 1, 0, 3) };
        slider.ValueChanged += (_, _) => label.Text = $"{name}    {slider.Value:0} {unit}";
        target.Children.Add(slider); return slider;
    }

    private static Border Card(UIElement content) => new() { Child = content, Background = Brush("#121D30"), BorderBrush = Brush("#24334B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20, 15, 20, 15), Margin = new Thickness(0, 17, 0, 0) };
    private static Button Button(string caption, string color = "#334155") => new() { Content = caption, Background = Brush(color), Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(0, 8, 8, 0), Cursor = System.Windows.Input.Cursors.Hand };
    private static TextBlock Text(string text, double size, string color, bool bold = false) => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) };
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
