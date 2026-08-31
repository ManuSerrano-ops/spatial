namespace PlanoOpenSpaceIT.Windows;

internal sealed class WindowInitialization<TStore, TBridge>
    where TStore : class
    where TBridge : class
{
    private readonly Func<TStore> _createStore;
    private readonly Func<TStore, TBridge> _createBridge;
    private readonly Action<TStore> _logLifecycleStart;
    private readonly Action _subscribeWebMessages;
    private bool _lifecycleStarted;
    private bool _webMessagesSubscribed;
    private bool _startupCompleted;

    internal WindowInitialization(
        Func<TStore> createStore,
        Func<TStore, TBridge> createBridge,
        Action<TStore> logLifecycleStart,
        Action subscribeWebMessages)
    {
        _createStore = createStore;
        _createBridge = createBridge;
        _logLifecycleStart = logLifecycleStart;
        _subscribeWebMessages = subscribeWebMessages;
    }

    internal TStore? Store { get; private set; }
    internal TBridge? Bridge { get; private set; }

    internal async Task InitializeAsync(Func<Action, Task> initializeWebViewAsync)
    {
        EnsureSession();
        if (_startupCompleted) return;

        await initializeWebViewAsync(EnsureWebMessagesSubscribed);
        _startupCompleted = true;
    }

    private void EnsureSession()
    {
        Store ??= _createStore();
        Bridge ??= _createBridge(Store);
        if (_lifecycleStarted) return;

        _logLifecycleStart(Store);
        _lifecycleStarted = true;
    }

    private void EnsureWebMessagesSubscribed()
    {
        if (_webMessagesSubscribed) return;

        _subscribeWebMessages();
        _webMessagesSubscribed = true;
    }
}
