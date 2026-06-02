using System.IO;
using Microsoft.Data.Sqlite;

namespace KeyboardTracker.Services;

public sealed class DatabaseService : IDisposable
{
    private readonly SqliteConnection _conn;

    public DatabaseService(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();

        using var pragma = _conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();

        CreateTables();
    }

    private void CreateTables()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS key_presses (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                vk_code     INTEGER NOT NULL,
                is_extended INTEGER NOT NULL DEFAULT 0,
                key_label   TEXT,
                press_count INTEGER NOT NULL DEFAULT 0,
                date        TEXT NOT NULL,
                hour        INTEGER NOT NULL,
                UNIQUE(vk_code, is_extended, date, hour)
            );

            CREATE TABLE IF NOT EXISTS mouse_clicks (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                button      TEXT NOT NULL,
                click_count INTEGER NOT NULL DEFAULT 0,
                date        TEXT NOT NULL,
                hour        INTEGER NOT NULL,
                UNIQUE(button, date, hour)
            );

            CREATE TABLE IF NOT EXISTS mouse_movement (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                distance_meters REAL NOT NULL DEFAULT 0,
                date            TEXT NOT NULL,
                hour            INTEGER NOT NULL,
                UNIQUE(date, hour)
            );

            CREATE TABLE IF NOT EXISTS active_time (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                seconds INTEGER NOT NULL DEFAULT 0,
                date    TEXT NOT NULL,
                hour    INTEGER NOT NULL,
                UNIQUE(date, hour)
            );

            CREATE TABLE IF NOT EXISTS daily_summary (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                date                TEXT NOT NULL UNIQUE,
                total_key_presses   INTEGER NOT NULL DEFAULT 0,
                total_mouse_clicks  INTEGER NOT NULL DEFAULT 0,
                total_mouse_distance REAL NOT NULL DEFAULT 0,
                active_seconds      INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_key_presses_lookup ON key_presses(date, hour);
            CREATE INDEX IF NOT EXISTS idx_mouse_clicks_lookup ON mouse_clicks(date, hour);
            CREATE INDEX IF NOT EXISTS idx_daily_summary_date ON daily_summary(date);

            CREATE TABLE IF NOT EXISTS minute_stats (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                keys        INTEGER NOT NULL DEFAULT 0,
                clicks      INTEGER NOT NULL DEFAULT 0,
                date        TEXT NOT NULL,
                hour        INTEGER NOT NULL,
                minute      INTEGER NOT NULL,
                UNIQUE(date, hour, minute)
            );
        ";
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection Connection => _conn;

    public void UpsertKeyPress(uint vkCode, bool isExtended, string keyLabel, long count, string date, int hour)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO key_presses (vk_code, is_extended, key_label, press_count, date, hour)
            VALUES (@vk, @ext, @label, @cnt, @date, @hour)
            ON CONFLICT(vk_code, is_extended, date, hour) DO UPDATE SET
                press_count = press_count + @cnt,
                key_label   = @label;
        ";
        cmd.Parameters.AddWithValue("@vk", (long)vkCode);
        cmd.Parameters.AddWithValue("@ext", isExtended ? 1L : 0L);
        cmd.Parameters.AddWithValue("@label", keyLabel);
        cmd.Parameters.AddWithValue("@cnt", count);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@hour", (long)hour);
        cmd.ExecuteNonQuery();
    }

    public void UpsertMouseClick(string button, long count, string date, int hour)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO mouse_clicks (button, click_count, date, hour)
            VALUES (@btn, @cnt, @date, @hour)
            ON CONFLICT(button, date, hour) DO UPDATE SET
                click_count = click_count + @cnt;
        ";
        cmd.Parameters.AddWithValue("@btn", button);
        cmd.Parameters.AddWithValue("@cnt", count);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@hour", (long)hour);
        cmd.ExecuteNonQuery();
    }

    public void UpsertMouseMovement(double distanceMeters, string date, int hour)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO mouse_movement (distance_meters, date, hour)
            VALUES (@dist, @date, @hour)
            ON CONFLICT(date, hour) DO UPDATE SET
                distance_meters = distance_meters + @dist;
        ";
        cmd.Parameters.AddWithValue("@dist", distanceMeters);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@hour", (long)hour);
        cmd.ExecuteNonQuery();
    }

    public void UpsertActiveTime(int seconds, string date, int hour)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO active_time (seconds, date, hour)
            VALUES (@sec, @date, @hour)
            ON CONFLICT(date, hour) DO UPDATE SET
                seconds = seconds + @sec;
        ";
        cmd.Parameters.AddWithValue("@sec", (long)seconds);
        cmd.Parameters.AddWithValue("@date", date);
        cmd.Parameters.AddWithValue("@hour", (long)hour);
        cmd.ExecuteNonQuery();
    }

    public void MergeDailySummary(string date)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO daily_summary (date, total_key_presses, total_mouse_clicks, total_mouse_distance, active_seconds)
            VALUES (
                @date,
                (SELECT COALESCE(SUM(press_count), 0) FROM key_presses WHERE date = @date),
                (SELECT COALESCE(SUM(click_count), 0) FROM mouse_clicks WHERE date = @date),
                (SELECT COALESCE(SUM(distance_meters), 0) FROM mouse_movement WHERE date = @date),
                (SELECT COALESCE(SUM(seconds), 0) FROM active_time WHERE date = @date)
            )
            ON CONFLICT(date) DO UPDATE SET
                total_key_presses   = excluded.total_key_presses,
                total_mouse_clicks  = excluded.total_mouse_clicks,
                total_mouse_distance = excluded.total_mouse_distance,
                active_seconds      = excluded.active_seconds;
        ";
        cmd.Parameters.AddWithValue("@date", date);
        cmd.ExecuteNonQuery();
    }

    public Dictionary<uint, long> GetKeyCountsByVk(string date)
    {
        var result = new Dictionary<uint, long>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT vk_code, SUM(press_count) FROM key_presses WHERE date = @date GROUP BY vk_code";
        cmd.Parameters.AddWithValue("@date", date);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result[(uint)reader.GetInt64(0)] = reader.GetInt64(1);
        return result;
    }

    public void UpsertMinuteStats(int keys, int clicks, string date, int hour, int minute)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO minute_stats (keys, clicks, date, hour, minute) VALUES (@k, @c, @d, @h, @m)
            ON CONFLICT(date, hour, minute) DO UPDATE SET keys = keys + @k, clicks = clicks + @c";
        cmd.Parameters.AddWithValue("@k", keys);
        cmd.Parameters.AddWithValue("@c", clicks);
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@h", hour);
        cmd.Parameters.AddWithValue("@m", minute);
        cmd.ExecuteNonQuery();
    }

    public List<(int Minute, double Keys, double Clicks)> GetMinuteStats(string date, int hour)
    {
        var r = new List<(int, double, double)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT minute, keys, clicks FROM minute_stats WHERE date=@d AND hour=@h ORDER BY minute";
        cmd.Parameters.AddWithValue("@d", date);
        cmd.Parameters.AddWithValue("@h", hour);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            r.Add((reader.GetInt32(0), reader.GetDouble(1), reader.GetDouble(2)));
        return r;
    }

    // ── Query helpers ──

    public List<(string KeyLabel, long Count)> GetKeyHeatmap(string date)
    {
        var result = new List<(string, long)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT key_label, SUM(press_count) FROM key_presses WHERE date = @date GROUP BY key_label ORDER BY SUM(press_count) DESC";
        cmd.Parameters.AddWithValue("@date", date);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1)));
        return result;
    }

    public List<(string Button, long Count)> GetMouseClickBreakdown(string date)
    {
        var result = new List<(string, long)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT button, SUM(click_count) FROM mouse_clicks WHERE date = @date GROUP BY button ORDER BY SUM(click_count) DESC";
        cmd.Parameters.AddWithValue("@date", date);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetString(0), reader.GetInt64(1)));
        return result;
    }

    public List<(int Hour, int Seconds)> GetHourlyActivity(string date)
    {
        var result = new List<(int, int)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT hour, seconds FROM active_time WHERE date = @date ORDER BY hour";
        cmd.Parameters.AddWithValue("@date", date);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetInt32(0), reader.GetInt32(1)));
        return result;
    }

    public List<(string Label, double Keys, double Clicks)> GetHourlyActivityDetail(string date)
    {
        var result = new List<(string, double, double)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT CAST(k.hour AS TEXT),
                   COALESCE(k.cnt, 0), COALESCE(m.cnt, 0)
            FROM (SELECT hour, SUM(press_count) AS cnt FROM key_presses WHERE date=@date GROUP BY hour) k
            LEFT JOIN (SELECT hour, SUM(click_count) AS cnt FROM mouse_clicks WHERE date=@date GROUP BY hour) m
            ON k.hour = m.hour
            ORDER BY k.hour";
        cmd.Parameters.AddWithValue("@date", date);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add((r.GetString(0), r.GetDouble(1), r.GetDouble(2)));
        return result;
    }

    public List<(int Minute, double Keys, double Clicks)> GetMinutelyActivity(string date, int hour)
    {
        // Minute-level requires raw event data — approximate from hourly data
        // For now return empty; we'll use hourly data divided
        return new();
    }

    public List<(string Month, double Keys, double Clicks)> GetMonthlyActivity(int year, int monthCount)
    {
        var result = new List<(string, double, double)>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT substr(date,1,7) AS m,
                   SUM(total_key_presses), SUM(total_mouse_clicks)
            FROM daily_summary
            WHERE date >= @start AND date <= @end
            GROUP BY m ORDER BY m";
        var start = new DateTime(year, 1, 1).AddMonths(-monthCount + 1).ToString("yyyy-MM-dd");
        var end = new DateTime(year, 12, 1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add((r.GetString(0), r.GetDouble(1), r.GetDouble(2)));
        return result;
    }

    public (long Keys, long Clicks, double Dist, int Active) GetTodayTotals(string date)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = @"
            SELECT
                (SELECT COALESCE(SUM(press_count), 0) FROM key_presses WHERE date = @date),
                (SELECT COALESCE(SUM(click_count), 0) FROM mouse_clicks WHERE date = @date),
                (SELECT COALESCE(SUM(distance_meters), 0) FROM mouse_movement WHERE date = @date),
                (SELECT COALESCE(SUM(seconds), 0) FROM active_time WHERE date = @date)
        ";
        cmd.Parameters.AddWithValue("@date", date);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return (reader.GetInt64(0), reader.GetInt64(1), reader.GetDouble(2), reader.GetInt32(3));
        return (0, 0, 0, 0);
    }

    public void Dispose()
    {
        _conn.Close();
        _conn.Dispose();
    }
}
