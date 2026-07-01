using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using LucideAvalonia;
using LucideAvalonia.Enum;

namespace TestApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        BuildIconGrid();
    }

    private void BuildIconGrid()
    {
        var root = new StackPanel { Margin = new Thickness(24), Spacing = 24 };

        // === SECTION 1: Interactive icon picker ===
        root.Children.Add(MakeHeader("ICON PICKER", "#a6e3a1"));
        root.Children.Add(MakeSubheader("Icon property supports data binding — select any icon from the dropdown"));
        root.Children.Add(MakeInteractiveDemo());

        root.Children.Add(MakeSeparator());

        // === SECTION 2: Sample icons ===
        var sampleIcons = new (LucideIconNames icon, string color)[]
        {
            (LucideIconNames.ArrowUp, "#cdd6f4"),
            (LucideIconNames.Heart, "#f38ba8"),
            (LucideIconNames.Star, "#f9e2af"),
            (LucideIconNames.Search, "#cdd6f4"),
            (LucideIconNames.Settings, "#cdd6f4"),
            (LucideIconNames.House, "#cdd6f4"),
            (LucideIconNames.Sun, "#f9e2af"),
            (LucideIconNames.Moon, "#89b4fa"),
            (LucideIconNames.Trash2, "#f38ba8"),
            (LucideIconNames.Camera, "#cdd6f4"),
            (LucideIconNames.Music, "#a6e3a1"),
            (LucideIconNames.Zap, "#f9e2af"),
            (LucideIconNames.Wifi, "#89b4fa"),
            (LucideIconNames.Globe, "#89dceb"),
        };

        root.Children.Add(MakeHeader("SAMPLE ICONS", "#89b4fa"));
        root.Children.Add(MakeIconRow(sampleIcons));

        root.Children.Add(MakeSeparator());

        // === SECTION 3: New icons from v1.0-v1.7.0 ===
        var newIcons = new (LucideIconNames icon, string color)[]
        {
            (LucideIconNames.Road, "#cdd6f4"),
            (LucideIconNames.LineStyle, "#cba6f7"),
            (LucideIconNames.ShieldCog, "#89b4fa"),
            (LucideIconNames.SportShoe, "#f9e2af"),
            (LucideIconNames.BeefOff, "#f38ba8"),
            (LucideIconNames.RadioOff, "#a6e3a1"),
            (LucideIconNames.CctvOff, "#89dceb"),
            (LucideIconNames.MapPinSearch, "#fab387"),
            (LucideIconNames.Balloon, "#f38ba8"),
            (LucideIconNames.ChessKing, "#cdd6f4"),
            (LucideIconNames.ChessQueen, "#cba6f7"),
            (LucideIconNames.Drone, "#a6e3a1"),
            (LucideIconNames.Helicopter, "#89dceb"),
            (LucideIconNames.Panda, "#cdd6f4"),
            (LucideIconNames.Shrimp, "#fab387"),
            (LucideIconNames.Volleyball, "#f9e2af"),
            (LucideIconNames.SolarPanel, "#a6e3a1"),
            (LucideIconNames.Gpu, "#89b4fa"),
            (LucideIconNames.Rose, "#f38ba8"),
            (LucideIconNames.Metronome, "#a6e3a1"),
        };

        root.Children.Add(MakeHeader("NEW ICONS (v1.0 — v1.7.0)", "#a6e3a1"));
        root.Children.Add(MakeIconRow(newIcons));

        Scroller.Content = root;
    }

    private static Control MakeInteractiveDemo()
    {
        var allIcons = System.Enum.GetValues<LucideIconNames>();
        var comboBox = new ComboBox
        {
            ItemsSource = allIcons,
            SelectedIndex = Array.IndexOf(allIcons, LucideIconNames.Rocket),
            Width = 200,
            Foreground = new SolidColorBrush(Color.Parse("#cdd6f4")),
            Background = new SolidColorBrush(Color.Parse("#313244")),
        };

        var slider = new Slider
        {
            Minimum = 0.5,
            Maximum = 1.5,
            Value = 1,
            Width = 200,
            Foreground = new SolidColorBrush(Color.Parse("#89b4fa")),
        };

        var lucide = new Lucide
        {
            Width = 64,
            Height = 64,
            StrokeBrush = new SolidColorBrush(Color.Parse("#cdd6f4")),
            HorizontalAlignment = HorizontalAlignment.Center,
            [!Lucide.IconProperty] = new Binding("SelectedItem") { Source = comboBox },
            [!Lucide.StrokeThicknessProperty] = new Binding("Value") { Source = slider },
        };

        var nameLabel = new TextBlock
        {
            FontSize = 14,
            Foreground = new SolidColorBrush(Color.Parse("#a6adc8")),
            HorizontalAlignment = HorizontalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding("SelectedItem") { Source = comboBox }
        };

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                comboBox,
                new TextBlock
                {
                    Text = "Thickness",
                    Foreground = new SolidColorBrush(Color.Parse("#6c7086")),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                slider,
            }
        };

        return new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { controls, lucide, nameLabel }
        };
    }

    private static TextBlock MakeHeader(string text, string color) =>
        new()
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse(color)),
            FontSize = 16,
            FontWeight = FontWeight.Bold,
        };

    private static TextBlock MakeSubheader(string text) =>
        new()
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse("#6c7086")),
            FontSize = 12,
            Margin = new Thickness(0, -16, 0, 0),
        };

    private static Border MakeSeparator() =>
        new() { Height = 1, Background = new SolidColorBrush(Color.Parse("#313244")), Margin = new Thickness(0, 8) };

    private static WrapPanel MakeIconRow((LucideIconNames icon, string color)[] icons)
    {
        var panel = new WrapPanel();
        foreach (var (icon, color) in icons)
        {
            var lucide = new Lucide
            {
                Icon = icon,
                StrokeBrush = new SolidColorBrush(Color.Parse(color)),
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var label = new TextBlock
            {
                Text = icon.ToString(),
                Foreground = new SolidColorBrush(Color.Parse("#6c7086")),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var stack = new StackPanel
            {
                Margin = new Thickness(8),
                Spacing = 4,
                Width = 90,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            stack.Children.Add(lucide);
            stack.Children.Add(label);
            panel.Children.Add(stack);
        }
        return panel;
    }
}
