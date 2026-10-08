namespace NICEDA.Web;

// Circuit-scoped demo identity only; the API remains responsible for authorization.
public sealed class DemoState
{
    private int _activeOperations;
    public string Mode { get; private set; } = "before";
    public string UserId { get; private set; } = "jane";
    public bool IsBusy => _activeOperations > 0;
    public event Action? Changed;

    public void SetMode(string mode)
    {
        if (!IsBusy && (mode is "before" or "after") && Mode != mode)
        {
            Mode = mode;
            Changed?.Invoke();
        }
    }

    public void SetUser(string userId)
    {
        if (!IsBusy && (userId is "jane" or "admin") && UserId != userId)
        {
            UserId = userId;
            Changed?.Invoke();
        }
    }

    public IDisposable BeginOperation()
    {
        _activeOperations++;
        Changed?.Invoke();
        return new Operation(this);
    }

    private sealed class Operation(DemoState state) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            state._activeOperations--;
            state.Changed?.Invoke();
        }
    }
}
