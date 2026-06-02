using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyboardTracker.Models;
using KeyboardTracker.Services;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace KeyboardTracker.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService? _db;

    public MainViewModel()
    {
        _db = ((App)Application.Current).Database;
        SelectedDate = DateTime.Today;
        RefreshCommand = new RelayCommand(Refresh);
        TodayCommand = new RelayCommand(() => { SelectedDate = DateTime.Today; Refresh(); });
        ChartModeCommand = new RelayCommand<string>(o => { if (int.TryParse(o, out var m)) ChartMode = m; });
        PrevDayCommand = new RelayCommand(() => { SelectedDate = SelectedDate.AddDays(-1); Refresh(); });
        NextDayCommand = new RelayCommand(() => { SelectedDate = SelectedDate.AddDays(1); Refresh(); });
        Refresh();

        // Auto-refresh every 5 seconds
        var timer = new System.Windows.Threading.DispatcherTimer(
            System.Windows.Threading.DispatcherPriority.Background,
            Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    private DateTime _selectedDate;
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set { _selectedDate = value; OnPropertyChanged(); }
    }

    public string TodayKeys { get; set; } = "0";
    public string TodayClicks { get; set; } = "0";
    public string TodayDistance { get; set; } = "0.0";
    public string TodayActive { get; set; } = "0h";
    public string LeftClicks { get; set; } = "0";
    public string RightClicks { get; set; } = "0";

    public ObservableCollection<TopKeyItem> TopKeys { get; } = new();
    public ObservableCollection<KbKey> KbKeys { get; } = new();
    public long MaxKeyCount { get; set; } = 1;
    public ObservableCollection<DailySummary> DailySummaries { get; } = new();

    public ObservableCollection<ISeries> ActivitySeries { get; } = new();
    private readonly ObservableCollection<double> _keysValues = new();
    private readonly ObservableCollection<double> _clicksValues = new();
    public Axis[] ActivityXAxes { get; set; } = [];
    public Axis[] ActivityYAxes { get; set; } = [];
    public bool HasActivity { get; set; }

    public string ChartModeText { get; set; } = "";
    private int _chartMode = 1; // 1=Hourly (has data by default)
    public int ChartMode
    {
        get => _chartMode;
        set { _chartMode = value; ChartModeText = value switch { 0=>"Minutely", 1=>"Hourly", 2=>"Daily", _=>"Monthly" }; RefreshChart(); OnPropertyChanged(nameof(ChartMode)); }
    }
    public Array ChartModes { get; } = new[] { "Minutely (1h)", "Hourly", "Daily (7d)", "Monthly (1y)" };
    public ICommand ChartModeCommand { get; }

    public ICommand RefreshCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand PrevDayCommand { get; }
    public ICommand NextDayCommand { get; }

    public void Refresh()
    {
        if (_db == null) return;
        var date = SelectedDate.ToString("yyyy-MM-dd");
        var (keys, clicks, dist, active) = _db.GetTodayTotals(date);

        TodayKeys = $"{keys:N0}";
        TodayClicks = $"{clicks:N0}";
        TodayDistance = $"{dist:F1}";
        TodayActive = $"{(active / 3600.0):F1}h";
        OnPropertyChanged(nameof(TodayKeys));
        OnPropertyChanged(nameof(TodayClicks));
        OnPropertyChanged(nameof(TodayDistance));
        OnPropertyChanged(nameof(TodayActive));

        var allKeys = _db.GetKeyHeatmap(date);
        var vkCounts = _db.GetKeyCountsByVk(date);
        MaxKeyCount = allKeys.Count > 0 ? allKeys[0].Count : 1;

        KbKeys.Clear();
        BuildKb();
        foreach (var k in KbKeys)
        {
            vkCounts.TryGetValue(k.VkCode, out var cnt);
            k.Count = cnt;
            k.Heat = MaxKeyCount > 0 ? Math.Clamp(Math.Sqrt((double)cnt / MaxKeyCount), 0, 1) : 0;
        }
        OnPropertyChanged(nameof(KbKeys));

        var clicksMap = new Dictionary<string, long>();
        foreach (var (btn, cnt) in _db.GetMouseClickBreakdown(date))
            clicksMap[btn] = cnt;
        LeftClicks = $"{clicksMap.GetValueOrDefault("left", 0):N0}";
        RightClicks = $"{clicksMap.GetValueOrDefault("right", 0):N0}";
        OnPropertyChanged(nameof(LeftClicks));
        OnPropertyChanged(nameof(RightClicks));

        RefreshChart();

        DailySummaries.Clear();
        for (int i = 6; i >= 0; i--)
        {
            var d = DateTime.Today.AddDays(-i);
            var ds = d.ToString("yyyy-MM-dd");
            var (dk, dc, dd, da) = _db.GetTodayTotals(ds);
            DailySummaries.Add(new DailySummary
            {
                Date = d.ToString("MM-dd"),
                TotalKeyPresses = dk,
                TotalMouseClicks = dc,
                TotalMouseDistance = dd,
                ActiveSeconds = da
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void RefreshChart()
    {
        if (_db == null) return;
        var date = SelectedDate.ToString("yyyy-MM-dd");

        double[]? kValues = null, cValues = null;
        string[]? labels = null;

        switch (_chartMode)
        {
            case 0: // Minutely
                {
                    var now = DateTime.Now;
                    var ms = _db.GetMinuteStats(date, now.Hour);
                    kValues = Enumerable.Range(0, 60).Select(m =>
                    { var item = ms.FirstOrDefault(x => x.Minute == m); return item.Minute == m ? item.Keys : 0; }).ToArray();
                    cValues = Enumerable.Range(0, 60).Select(m =>
                    { var item = ms.FirstOrDefault(x => x.Minute == m); return item.Minute == m ? item.Clicks : 0; }).ToArray();
                    labels = Enumerable.Range(0, 60).Select(m => m % 10 == 0 ? $"{m}m" : "").ToArray();
                    break;
                }
            case 1:
                var hd = _db.GetHourlyActivityDetail(date);
                kValues = Enumerable.Range(0, 24).Select(h => {
                    var item = hd.FirstOrDefault(x => int.TryParse(x.Label, out var lh) && lh == h);
                    return item.Label != null ? item.Keys : 0;
                }).ToArray();
                cValues = Enumerable.Range(0, 24).Select(h => {
                    var item = hd.FirstOrDefault(x => int.TryParse(x.Label, out var lh) && lh == h);
                    return item.Label != null ? item.Clicks : 0;
                }).ToArray();
                labels = Enumerable.Range(0, 24).Select(i => i % 3 == 0 ? $"{i}h" : "").ToArray();
                break;
            case 2:
                var daily = new List<(string, double, double)>();
                for (int i = 6; i >= 0; i--)
                {
                    var d = DateTime.Today.AddDays(-i).ToString("yyyy-MM-dd");
                    var (k, c, _, _) = _db.GetTodayTotals(d);
                    daily.Add((d, k, c));
                }
                kValues = daily.Select(x => x.Item2).ToArray();
                cValues = daily.Select(x => x.Item3).ToArray();
                labels = daily.Select(x => DateTime.Parse(x.Item1).ToString("MM/dd")).ToArray();
                break;
            case 3:
                var mo = _db.GetMonthlyActivity(DateTime.Now.Year, 12);
                kValues = mo.Select(x => x.Keys).ToArray();
                cValues = mo.Select(x => x.Clicks).ToArray();
                labels = mo.Select(x => x.Month).ToArray();
                break;
        }

        HasActivity = (kValues?.Any(v => v > 0) ?? false) || (cValues?.Any(v => v > 0) ?? false);

        // Create once, update in-place
        if (ActivitySeries.Count == 0)
        {
            ActivitySeries.Add(new LineSeries<double>
            {
                Name = "Keys",
                Values = _keysValues,
                Stroke = new SolidColorPaint(new SKColor(0x42, 0x7B, 0xD4), 3),
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometryStroke = new SolidColorPaint(new SKColor(0x42, 0x7B, 0xD4), 2),
                GeometrySize = 6, LineSmoothness = 0.3,
            });
            ActivitySeries.Add(new LineSeries<double>
            {
                Name = "Clicks",
                Values = _clicksValues,
                Stroke = new SolidColorPaint(new SKColor(0x34, 0xC7, 0x59), 3),
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometryStroke = new SolidColorPaint(new SKColor(0x34, 0xC7, 0x59), 2),
                GeometrySize = 6, LineSmoothness = 0.3,
            });
            ActivityXAxes = [new Axis { Labels = labels! }];
            ActivityYAxes = [new Axis { MinLimit = 0 }];
            OnPropertyChanged(nameof(ActivityXAxes));
            OnPropertyChanged(nameof(ActivityYAxes));
        }
        else
        {
            if (labels != null) ActivityXAxes[0].Labels = labels;
        }

        // Update values in-place
        _keysValues.Clear();
        _clicksValues.Clear();
        foreach (var v in kValues!) _keysValues.Add(v);
        foreach (var v in cValues!) _clicksValues.Add(v);

        OnPropertyChanged(nameof(ActivitySeries));
        OnPropertyChanged(nameof(ChartMode));
    }

    private void BuildKb()
    {
        const int W = 30;

        AddRow(0, 0, ("Esc",2,0x1B),("",1,0),("F1",2,0x70),("F2",2,0x71),("F3",2,0x72),("F4",2,0x73), ("",1,0),
               ("F5",2,0x74),("F6",2,0x75),("F7",2,0x76),("F8",2,0x77), ("",1,0),
               ("F9",2,0x78),("F10",2,0x79),("F11",2,0x7A),("F12",3,0x7B));

        AddRow(1, 0, ("`",2,0xC0),("1",2,0x31),("2",2,0x32),("3",2,0x33),("4",2,0x34),("5",2,0x35),("6",2,0x36),("7",2,0x37),("8",2,0x38),("9",2,0x39),("0",2,0x30),("-",2,0xBD),("=",2,0xBB),("Backspace",4,0x08));

        AddRow(2, 0, ("Tab",3,0x09),("Q",2,0x51),("W",2,0x57),("E",2,0x45),("R",2,0x52),("T",2,0x54),("Y",2,0x59),("U",2,0x55),("I",2,0x49),("O",2,0x4F),("P",2,0x50),("[",2,0xDB),("]",2,0xDD),("\\",3,0xDC));

        AddRow(3, 0, ("CapsLock",4,0x14),("A",2,0x41),("S",2,0x53),("D",2,0x44),("F",2,0x46),("G",2,0x47),("H",2,0x48),("J",2,0x4A),("K",2,0x4B),("L",2,0x4C),(";",2,0xBA),("'",2,0xDE),("Enter",4,0x0D));

        AddRow(4, 0, ("LShift",5,0xA0),("Z",2,0x5A),("X",2,0x58),("C",2,0x43),("V",2,0x56),("B",2,0x42),("N",2,0x4E),("M",2,0x4D),(",",2,0xBC),(".",2,0xBE),("/",2,0xBF),("RShift",5,0xA1));

        AddRow(5, 0, ("LCtrl",2,0xA2),("LWin",2,0x5B),("LAlt",2,0xA4),("Space",16,0x20),("RAlt",2,0xA5),("RWin",2,0x5C),("Menu",2,0x5D),("RCtrl",2,0xA3));

        // Edit cluster
        AddRow(0, W+1, ("PrintScreen",3,0x2C),("ScrollLock",3,0x91),("Pause",3,0x13));
        AddRow(1, W+1, ("Insert",3,0x2D),("Home",3,0x24),("PageUp",3,0x21));
        AddRow(2, W+1, ("Delete",3,0x2E),("End",3,0x23),("PageDown",3,0x22));
        AddRow(4, W+1, ("",3,0),("Up",3,0x26),("",3,0));
        AddRow(5, W+1, ("Left",3,0x25),("Down",3,0x28),("Right",3,0x27));
    }

    private void AddRow(int row, int startCol, params (string label, int colSpan, uint vk)[] keys)
    {
        int col = startCol;
        foreach (var (label, span, vk) in keys)
        {
            KbKeys.Add(new KbKey { Label = label, Row = row, Col = col, ColSpan = span, VkCode = vk });
            col += span;
        }
    }
}

public sealed class KbKey : INotifyPropertyChanged
{
    public string Label { get; set; } = "";
    public uint VkCode { get; set; }
    public int Row { get; set; }
    public int Col { get; set; }
    public int ColSpan { get; set; } = 1;
    public long Count { get; set; }
    public double Heat { get; set; }
    public Brush KeyBg
    {
        get
        {
            double t = Heat;
            if (t <= 0) return new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xED));
            // Match legend: #E3F2FD → #90CAF9 → #42A5F5 → #1E88E5 → #0D47A1
            byte r, g, b;
            if (t < 0.25)
            {
                double s = t / 0.25;
                r = (byte)(0xE3 + s * (0x90 - 0xE3)); g = (byte)(0xF2 + s * (0xCA - 0xF2)); b = (byte)(0xFD + s * (0xF9 - 0xFD));
            }
            else if (t < 0.5)
            {
                double s = (t - 0.25) / 0.25;
                r = (byte)(0x90 + s * (0x42 - 0x90)); g = (byte)(0xCA + s * (0xA5 - 0xCA)); b = (byte)(0xF9 + s * (0xF5 - 0xF9));
            }
            else if (t < 0.75)
            {
                double s = (t - 0.5) / 0.25;
                r = (byte)(0x42 + s * (0x1E - 0x42)); g = (byte)(0xA5 + s * (0x88 - 0xA5)); b = (byte)(0xF5 + s * (0xE5 - 0xF5));
            }
            else
            {
                double s = (t - 0.75) / 0.25;
                r = (byte)(0x1E + s * (0x0D - 0x1E)); g = (byte)(0x88 + s * (0x47 - 0x88)); b = (byte)(0xE5 + s * (0xA1 - 0xE5));
            }
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }
    public Brush KeyFg => Heat > 0.4 ? Brushes.White
        : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
    public string Tooltip => string.IsNullOrEmpty(Label) ? "" : $"{Label}: {Count:N0}";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TopKeyItem : INotifyPropertyChanged
{
    public int Rank { get; set; }
    public string Label { get; set; } = "";
    public long Count { get; set; }
    public long MaxCount { get; set; } = 1;
    public double Ratio => MaxCount > 0 ? (double)Count / MaxCount : 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

public sealed class BarWidthConverter : System.Windows.Data.IValueConverter
{
    public static readonly BarWidthConverter Instance = new();
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        => value is double ratio ? Math.Max(ratio * 300, 3) : 3;
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

public sealed class ModeBtnStyleConverter : System.Windows.Data.IValueConverter
{
    public static readonly ModeBtnStyleConverter Instance = new();
    private static readonly Style NormalStyle = new(typeof(Button))
    {
        Setters = {
            new Setter(Button.WidthProperty, 64.0),
            new Setter(Button.HeightProperty, 28.0),
            new Setter(Button.FontSizeProperty, 11.0),
            new Setter(Button.MarginProperty, new Thickness(2,0,0,0)),
            new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand),
            new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF0,0xF0,0xF5))),
            new Setter(Button.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0xD0,0xD0,0xDA))),
            new Setter(Button.BorderThicknessProperty, new Thickness(1)),
            new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x6B,0x72,0x80))),
        }
    };
    private static readonly Style ActiveStyle = new(typeof(Button))
    {
        Setters = {
            new Setter(Button.WidthProperty, 64.0),
            new Setter(Button.HeightProperty, 28.0),
            new Setter(Button.FontSizeProperty, 11.0),
            new Setter(Button.MarginProperty, new Thickness(2,0,0,0)),
            new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand),
            new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x42,0x7B,0xD4))),
            new Setter(Button.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x42,0x7B,0xD4))),
            new Setter(Button.BorderThicknessProperty, new Thickness(1)),
            new Setter(Button.ForegroundProperty, Brushes.White),
        }
    };
    public object Convert(object value, Type t, object parameter, System.Globalization.CultureInfo c)
    {
        if (value is int mode && parameter is string s && int.TryParse(s, out var p))
            return mode == p ? ActiveStyle : NormalStyle;
        return NormalStyle;
    }
    public object ConvertBack(object value, Type t, object parameter, System.Globalization.CultureInfo c) => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : System.Windows.Data.IValueConverter
{
    public static readonly InverseBoolConverter Instance = new();
    public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c)
        => throw new NotSupportedException();
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => _execute();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    public RelayCommand(Action<T?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => _execute((T?)p);
}
