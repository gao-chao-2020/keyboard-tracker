using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using KeyboardTracker.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Binding = System.Windows.Data.Binding;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace KeyboardTracker.Views;

public partial class DashboardView : UserControl
{
    private bool _kbBuilt;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.KbKeys) || args.PropertyName == null)
                    BuildKeyboard(vm);
            };
            BuildKeyboard(vm);
        }
    }

    private void BuildKeyboard(MainViewModel vm)
    {
        if (vm.KbKeys.Count == 0) return;

        KeyboardGrid.Children.Clear();
        KeyboardGrid.ColumnDefinitions.Clear();
        KeyboardGrid.RowDefinitions.Clear();

        // Determine max columns needed
        int maxCol = 0;
        foreach (var k in vm.KbKeys)
        {
            int right = k.Col + k.ColSpan;
            if (right > maxCol) maxCol = right;
        }

        // 0.5u per column, 1u = 2 cols, 1u = 36px → each col = 18px
        double colWidth = 18;
        for (int i = 0; i < maxCol; i++)
            KeyboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidth) });

        // 7 rows, each = 44px
        for (int i = 0; i < 7; i++)
            KeyboardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

        foreach (var k in vm.KbKeys)
        {
            if (string.IsNullOrEmpty(k.Label))
            {
                // Spacer — just skip rendering
                continue;
            }

            var border = new Border
            {
                Height = 38,
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)),
                BorderThickness = new Thickness(0.5),
                ToolTip = k.Tooltip,
            };

            // Background binding
            var bgBinding = new Binding(nameof(KbKey.KeyBg))
            {
                Source = k,
                Mode = BindingMode.OneWay
            };
            border.SetBinding(Border.BackgroundProperty, bgBinding);

            var inner = new Border
            {
                Margin = new Thickness(1),
                CornerRadius = new CornerRadius(3),
            };
            inner.SetBinding(Border.BackgroundProperty, bgBinding);

            var text = new TextBlock
            {
                Text = GetDisplayLabel(k.Label),
                FontSize = k.Label switch { "Backspace" => 14, "Up" or "Down" or "Left" or "Right" => 16, _ => 12 },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var fgBinding = new Binding(nameof(KbKey.KeyFg))
            {
                Source = k,
                Mode = BindingMode.OneWay
            };
            text.SetBinding(TextBlock.ForegroundProperty, fgBinding);

            inner.Child = text;
            border.Child = inner;

            Grid.SetRow(border, k.Row);
            Grid.SetColumn(border, k.Col);
            Grid.SetColumnSpan(border, k.ColSpan);
            KeyboardGrid.Children.Add(border);
        }
    }

    private void OnSegmentedControlLoaded(object sender, RoutedEventArgs e)
    {
        // Apply initial active style (1d = Tag 1 = ChartMode 1)
        var activeBtn = SegmentedControl.Children.OfType<Button>().FirstOrDefault(b => (string)b.Tag == "1");
        if (activeBtn != null) ApplySegmentedStyle(activeBtn);
    }

    private void OnChartModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && DataContext is MainViewModel vm)
        {
            vm.ChartMode = int.Parse((string)btn.Tag);
            ApplySegmentedStyle(btn);
        }
    }

    private void ApplySegmentedStyle(Button activeBtn)
    {
        var activeBg = new SolidColorBrush(Color.FromRgb(0x42, 0x7B, 0xD4));
        var normalBg = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF5));
        var normalFg = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        foreach (Button child in SegmentedControl.Children.OfType<Button>())
        {
            bool active = child == activeBtn;
            child.Background = active ? activeBg : normalBg;
            child.Foreground = active ? Brushes.White : normalFg;
            child.BorderThickness = new Thickness(0);
            child.FontSize = 12;
            child.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
            child.Padding = new Thickness(12, 4, 12, 4);
            child.Cursor = Cursors.Hand;
        }
    }

    private static string GetDisplayLabel(string label) => label switch
    {
        "Backspace" => "←",
        "CapsLock" => "Caps",
        "LShift" or "RShift" => "Shift",
        "LCtrl" or "RCtrl" => "Ctrl",
        "LWin" or "RWin" => "Win",
        "LAlt" or "RAlt" => "Alt",
        "PrintScreen" => "PrtSc",
        "ScrollLock" => "ScrLk",
        "Insert" => "Ins",
        "Delete" => "Del",
        "PageUp" => "PgUp",
        "PageDown" => "PgDn",
        "Up" => "↑",
        "Down" => "↓",
        "Left" => "←",
        "Right" => "→",
        "Menu" => "▤",
        _ => label,
    };
}
