namespace KeyboardTracker.Models;

public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2
}

public abstract record InputEvent
{
    public uint TimeMs { get; init; }
}

public sealed record KeyEvent : InputEvent
{
    public uint VkCode { get; init; }
    public uint ScanCode { get; init; }
    public bool IsExtended { get; init; }
}

public sealed record MouseClickEvent : InputEvent
{
    public MouseButton Button { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
}

public sealed record MouseMoveEvent : InputEvent
{
    public int X { get; init; }
    public int Y { get; init; }
}
