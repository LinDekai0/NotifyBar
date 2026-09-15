using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using NotificationBarrage.Domain;

namespace NotificationBarrage.UI;

public sealed class BarrageItemControl : Border
{
    public BarrageItemControl(BarrageMessage message, AppSettings settings, double maximumWidth)
    {
        Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42));
        CornerRadius = new CornerRadius(10);
        Padding = new Thickness(13, 7, 13, 7);
        Opacity = settings.Opacity;
        MaxWidth = Math.Max(100, maximumWidth);
        Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Opacity = .5 };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var isQQ = message.Source == "QQ";
        row.Children.Add(new TextBlock { Text = isQQ ? "QQ" : "微信", Foreground = new SolidColorBrush(isQQ ? Color.FromRgb(93, 193, 255) : Color.FromRgb(78, 222, 158)), FontSize = settings.FontSize, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 12, 0) });
        var text = string.IsNullOrEmpty(message.Title) ? message.Body : string.IsNullOrEmpty(message.Body) ? message.Title : $"{message.Title}：{message.Body}";
        row.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontSize = settings.FontSize, FontFamily = new FontFamily("Microsoft YaHei UI"), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = Math.Max(60, maximumWidth - settings.FontSize * 3 - 40) });
        Child = row;
    }
}
