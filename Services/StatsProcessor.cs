using System.Threading.Channels;
using KeyboardTracker.Models;

namespace KeyboardTracker.Services;

public sealed class StatsProcessor : IDisposable
{
    private readonly DatabaseService _db;
    private readonly ChannelReader<InputEvent> _reader;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _task;

    // In-memory hourly buffer
    private readonly Dictionary<(uint VkCode, bool IsExtended), long> _keyCounts = new();
    private readonly Dictionary<string, long> _clickCounts = new();
    private double _pixelDistance;
    private (int X, int Y)? _lastMousePos;
    private DateTime _lastEventTime = DateTime.MinValue;
    private int _activeSeconds;
    private (string Date, int Hour)? _currentHour;
    private DateTime _lastFlush = DateTime.UtcNow;

    // Per-minute tracking
    private int _minuteKeys;
    private int _minuteClicks;
    private int _currentMinute = -1;

    private const double PixelsPerMeter = 96.0 / 0.0254;

    public StatsProcessor(DatabaseService db, ChannelReader<InputEvent> reader)
    {
        _db = db;
        _reader = reader;
        _task = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        Console.WriteLine("[StatsProcessor] Started.");
        try
        {
            while (await _reader.WaitToReadAsync(_cts.Token))
            {
                while (_reader.TryRead(out var evt))
                {
                    ProcessEvent(evt);
                }

                // Flush every 5 seconds
                var now = DateTime.UtcNow;
                if ((now - _lastFlush).TotalSeconds >= 5)
                {
                    Flush();
                    _lastFlush = now;
                }
            }
        }
        catch (OperationCanceledException) { }

        // Final flush
        Flush();
        Console.WriteLine("[StatsProcessor] Stopped.");
    }

    private void ProcessEvent(InputEvent evt)
    {
        var utcNow = DateTime.UtcNow;
        var localNow = DateTime.Now;
        var date = localNow.ToString("yyyy-MM-dd");
        var hour = localNow.Hour;
        var minute = localNow.Minute;

        // Check minute rollover
        if (_currentMinute != minute)
        {
            if (_currentMinute >= 0)
                FlushMinute(date, hour, _currentMinute);
            _currentMinute = minute;
            _minuteKeys = 0;
            _minuteClicks = 0;
        }

        // Check hour rollover
        var hourKey = (date, hour);
        if (_currentHour != null && _currentHour != hourKey)
        {
            Flush();
            _currentHour = hourKey;
        }
        _currentHour ??= hourKey;

        // Active time
        if (_lastEventTime != DateTime.MinValue)
        {
            var gap = (utcNow - _lastEventTime).TotalSeconds;
            if (gap <= 300) // ≤ 5 min gap counts as continuous
                _activeSeconds += Math.Min((int)gap, 300);
        }
        _lastEventTime = utcNow;

        switch (evt)
        {
            case KeyEvent k:
                {
                    var key = (k.VkCode, k.IsExtended);
                    _keyCounts.TryGetValue(key, out var cnt);
                    _keyCounts[key] = cnt + 1;
                    _minuteKeys++;
                    break;
                }

            case MouseClickEvent mc:
                {
                    var btn = mc.Button.ToString().ToLowerInvariant();
                    _clickCounts.TryGetValue(btn, out var cnt);
                    _clickCounts[btn] = cnt + 1;
                    _minuteClicks++;
                    break;
                }

            case MouseMoveEvent mm:
                {
                    if (_lastMousePos.HasValue)
                    {
                        var dx = mm.X - _lastMousePos.Value.X;
                        var dy = mm.Y - _lastMousePos.Value.Y;
                        _pixelDistance += Math.Sqrt(dx * dx + dy * dy);
                    }
                    _lastMousePos = (mm.X, mm.Y);
                    break;
                }
        }
    }

    private void Flush()
    {
        if (_currentHour == null) return;

        var date = _currentHour.Value.Date;
        var hour = _currentHour.Value.Hour;

        // Key presses
        foreach (var (key, count) in _keyCounts)
        {
            if (count == 0) continue;
            var label = KeyLabelService.GetLabel(key.VkCode, key.IsExtended);
            _db.UpsertKeyPress(key.VkCode, key.IsExtended, label, count, date, hour);
        }
        _keyCounts.Clear();

        // Mouse clicks
        foreach (var (button, count) in _clickCounts)
        {
            if (count == 0) continue;
            _db.UpsertMouseClick(button, count, date, hour);
        }
        _clickCounts.Clear();

        // Mouse movement
        if (_pixelDistance > 0)
        {
            _db.UpsertMouseMovement(_pixelDistance / PixelsPerMeter, date, hour);
            _pixelDistance = 0;
        }

        // Active time
        if (_activeSeconds > 0)
        {
            _db.UpsertActiveTime(_activeSeconds, date, hour);
            _activeSeconds = 0;
        }

        // Update daily summary
        _db.MergeDailySummary(date);

        Console.WriteLine($"[StatsProcessor] Flushed hour {date}T{hour:D2}");
    }

    private void FlushMinute(string date, int hour, int minute)
    {
        if (_minuteKeys > 0 || _minuteClicks > 0)
            _db.UpsertMinuteStats(_minuteKeys, _minuteClicks, date, hour, minute);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
