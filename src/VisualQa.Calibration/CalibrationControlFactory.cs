using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;

namespace VisualQa.Calibration;
public static class CalibrationControlFactory
{
    public static FrameworkElement Create(string variant)
    {
        var card = new Border { Width = 360, Height = 180, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(210, 218, 230)), BorderThickness = new Thickness(1), Padding = new Thickness(24), CornerRadius = new CornerRadius(8) };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var avatar = new Border { Width = 56, Height = 56, Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)), CornerRadius = new CornerRadius(28), Child = new TextBlock { Text = variant == "wrong-icon" ? "!" : "AB", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        var title = new TextBlock { Text = variant == "missing-text" ? "" : "Avery Brooks", FontFamily = new FontFamily("Segoe UI"), FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(20, 33, 61)) };
        var subtitle = new TextBlock { Text = "Primary patient", FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 5, 0, 0) };
        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; textStack.Children.Add(title); textStack.Children.Add(subtitle);
        var statusDot = new Border { Width = 10, Height = 10, Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)), CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetColumn(avatar, 0); Grid.SetColumn(textStack, 2); grid.Children.Add(avatar); grid.Children.Add(textStack); grid.Children.Add(statusDot); card.Child = grid;
        Apply(variant, card, grid, avatar, title, subtitle, textStack, statusDot);
        return card;
    }
    private static void Apply(string variant, Border card, Grid grid, Border avatar, TextBlock title, TextBlock subtitle, StackPanel text, Border statusDot)
    {
        switch (variant)
        {
            case "exact": break;
            case "x1": statusDot.RenderTransform = new TranslateTransform(1, 0); break;
            case "x4": avatar.Margin = new Thickness(4, 0, 0, 0); break;
            case "width4": avatar.Width += 4; break;
            case "height4": avatar.Height += 4; card.Padding = new Thickness(24, 24, 24, 28); break;
            case "wrong-foreground": title.Foreground = Brushes.Firebrick; break;
            case "subtle-foreground": subtitle.Foreground = new SolidColorBrush(Color.FromRgb(75, 88, 110)); break;
            case "font-size": title.FontSize = 22; break;
            case "font-weight": title.FontWeight = FontWeights.Bold; break;
            case "line-height": subtitle.LineHeight = 24; break;
            case "letter-spacing": title.FontFamily = new FontFamily("Consolas"); break;
            case "missing-icon": avatar.Visibility = Visibility.Hidden; break;
            case "wrong-icon": avatar.Background = Brushes.OrangeRed; break;
            case "missing-text": break;
            case "extra-element": grid.Children.Add(new Border { Width = 18, Height = 18, Background = Brushes.OrangeRed, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top }); break;
            case "gap": Grid.SetColumn(text, 2); text.Margin = new Thickness(16, 0, 0, 0); break;
            case "padding": card.Padding = new Thickness(40, 24, 24, 24); break;
            case "clipping": card.ClipToBounds = true; title.Width = 42; break;
            case "background": card.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); break;
            case "anti-alias": statusDot.Opacity = .97; break;
            case "dpi": card.LayoutTransform = new ScaleTransform(1.03, 1.03); break;
            case "component-width": avatar.Width = 40; break;
            case "invisible": text.Visibility = Visibility.Hidden; break;
            case "vertical-alignment": text.VerticalAlignment = VerticalAlignment.Bottom; break;
            default: throw new InvalidDataException($"Unknown calibration variant '{variant}'.");
        }
    }
}
