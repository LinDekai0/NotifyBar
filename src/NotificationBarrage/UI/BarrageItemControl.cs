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
        row.Children.Add(new TextBlock
        {
            Text = message.Source,
            Foreground = new SolidColorBrush(Color.FromRgb(93, 193, 255)),
            FontSize = settings.FontSize,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 12, 0),
            MaxWidth = Math.Min(180, Math.Max(70, maximumWidth * .22)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var text = string.IsNullOrEmpty(message.Title) ? message.Body : string.IsNullOrEmpty(message.Body) ? message.Title : $"{message.Title}：{message.Body}";
        row.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, FontSize = settings.FontSize, FontFamily = new FontFamily("Microsoft YaHei UI"), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = Math.Max(60, maximumWidth - Math.Min(180, Math.Max(70, maximumWidth * .22)) - 40) });
        Child = row;
    }
}
