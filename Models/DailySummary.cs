namespace KeyboardTracker.Models;

public sealed record DailySummary
{
    public string Date { get; init; } = "";
    public long TotalKeyPresses { get; init; }
    public long TotalMouseClicks { get; init; }
    public double TotalMouseDistance { get; init; }
    public int ActiveSeconds { get; init; }

    public string ActiveTimeFormatted => $"{ActiveSeconds / 3600:F1}h";
}
