using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using NotificationBarrage.Domain;

namespace NotificationBarrage.UI;

public sealed class OverlayWindow : Window
{
    private readonly Canvas _canvas = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly Dictionary<int, BarrageItemControl> _items = new();
    private AppSettings _settings = new();
    private OverlayGeometry _geometry = new(0, 58, 5);
    public event EventHandler? MessageCompleted;
    public int ActiveItems => _items.Count;
    public int AvailableSlots => _geometry.LaneCount - _items.Count;

    public OverlayWindow()
    {
        Title = "NotifyBar 覆盖层";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        Content = _canvas;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLongPtr(handle, -20, GetWindowLongPtr(handle, -20) | 0x80000 | 0x20 | 0x80 | 0x08000000);
            HwndSource.FromHwnd(handle)?.AddHook(WindowHook);
            ApplySettings(_settings);
        };
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Closed += (_, _) => { SystemEvents.DisplaySettingsChanged -= DisplayChanged; ClearMessages(); };
    }

    public void ApplySettings(AppSettings settings)
    {
        ClearMessages();
        _settings = settings.Normalize();
        var bounds = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var origin = transform.Transform(new Point(bounds.Left, bounds.Top));
        var size = transform.Transform(new Point(bounds.Width, bounds.Height));
        var screenGeometry = OverlayLayout.Calculate(size.Y, _settings);
        var viewport = OverlayViewport.Calculate(size.X, size.Y, screenGeometry);
        Left = origin.X; Top = origin.Y + viewport.Top; Width = viewport.Width; Height = viewport.Height;
        _geometry = screenGeometry with { BandTop = viewport.BandTop };
    }

    public bool ShowMessage(BarrageMessage message, AppSettings settings)
    {
        var lane = Enumerable.Range(0, _geometry.LaneCount).FirstOrDefault(i => !_items.ContainsKey(i), -1);
        if (lane < 0) return false;
        var item = new BarrageItemControl(message, settings, Math.Max(160, Width * .85));
        item.CacheMode = new BitmapCache { RenderAtScale = VisualTreeHelper.GetDpi(this).DpiScaleX };
        item.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var itemWidth = item.DesiredSize.Width;
        var move = new TranslateTransform(Width, _geometry.BandTop + lane * _geometry.LaneHeight);
        item.RenderTransform = move;
        _items.Add(lane, item);
        _canvas.Children.Add(item);
        var seconds = (Width + itemWidth) / settings.Normalize().SpeedPixelsPerSecond;
        var animation = new DoubleAnimation(Width, -itemWidth, TimeSpan.FromSeconds(seconds));
        animation.Completed += (_, _) => Complete(lane, item);
        try
        {
            move.BeginAnimation(TranslateTransform.XProperty, animation);
            item.BeginAnimation(OpacityProperty, new DoubleAnimation(settings.Opacity, 0, TimeSpan.FromSeconds(Math.Min(.6, seconds / 3))) { BeginTime = TimeSpan.FromSeconds(Math.Max(0, seconds - .6)) });
        }
        catch
        {
            _items.Remove(lane); _canvas.Children.Remove(item);
            move.BeginAnimation(TranslateTransform.XProperty, null);
            throw; // No acceptance was returned; the host releases the queue slot.
        }
        return true;
    }

    private void Complete(int lane, BarrageItemControl item)
    {
        if (!_items.TryGetValue(lane, out var current) || !ReferenceEquals(item, current)) return;
        _items.Remove(lane);
        ((TranslateTransform)item.RenderTransform).BeginAnimation(TranslateTransform.XProperty, null);
        item.BeginAnimation(OpacityProperty, null);
        _canvas.Children.Remove(item);
        MessageCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void ClearMessages() { foreach (var pair in _items.ToArray()) Complete(pair.Key, pair.Value); }
    public object Diagnostics()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = (long)GetWindowLongPtr(handle, -20);
        return new { Width, Height, Topmost, ShowActivated, ActiveItems, ClickThrough = (style & 0x20) != 0, NoActivate = (style & 0x08000000) != 0, ToolWindow = (style & 0x80) != 0, BandTop = _geometry.BandTop, LaneCount = _geometry.LaneCount };
    }
    private void DisplayChanged(object? sender, EventArgs args) => Dispatcher.BeginInvoke(() => ApplySettings(_settings));
    private IntPtr WindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x84) { handled = true; return new IntPtr(-1); }
        if (msg == 0x21) { handled = true; return new IntPtr(3); }
        if (msg == 0x02E0) Dispatcher.BeginInvoke(() => ApplySettings(_settings));
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint window, int index, nint value);
}
