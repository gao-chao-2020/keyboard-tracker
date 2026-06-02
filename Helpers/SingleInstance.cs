namespace KeyboardTracker.Helpers;

public sealed class SingleInstance : IDisposable
{
    private Mutex? _mutex;
    public bool IsFirstInstance { get; }

    public SingleInstance(string name)
    {
        try
        {
            _mutex = Mutex.OpenExisting(name);
            IsFirstInstance = false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            _mutex = new Mutex(initiallyOwned: true, name);
            IsFirstInstance = true;
        }
    }

    public void Dispose()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Close();
        _mutex = null;
    }
}
