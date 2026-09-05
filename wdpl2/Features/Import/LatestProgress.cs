namespace Wdpl2.Services.Import;

// Producers never queue UI callbacks; the consumer samples the latest report.
public sealed class LatestProgress<T> : IProgress<T>, IDisposable where T : class
{
    private readonly object _gate = new();
    private T? _latest;
    private bool _disposed;

    public void Report(T value)
    {
        lock (_gate)
        {
            if (!_disposed)
                _latest = value;
        }
    }

    public T? TakeLatest()
    {
        lock (_gate)
        {
            var value = _latest;
            _latest = null;
            return value;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _latest = null;
        }
    }
}
