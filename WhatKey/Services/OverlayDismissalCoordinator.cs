namespace WhatKey.Services;

/// <summary>
/// Keeps one overlay dismissal timer active at a time. A newer request cancels
/// the previous timer instead of adding another display duration to the queue.
/// </summary>
internal sealed class OverlayDismissalCoordinator : IDisposable
{
    private readonly object _gate = new();
    private DismissalRequest? _currentRequest;
    private int _disposed;

    public async Task RunAsync(TimeSpan duration, Func<Task> onElapsed)
    {
        DismissalRequest currentRequest;
        DismissalRequest? previousRequest;
        lock (_gate)
        {
            if (_disposed != 0)
                return;

            currentRequest = new DismissalRequest();
            previousRequest = _currentRequest;
            _currentRequest = currentRequest;
            if (previousRequest is not null)
                previousRequest.IsSuperseded = true;
        }

        try
        {
            if (previousRequest is not null)
            {
                await previousRequest.Cancellation.CancelAsync();
                previousRequest.Cancellation.Dispose();
            }

            lock (_gate)
            {
                if (_disposed != 0 || !ReferenceEquals(_currentRequest, currentRequest))
                    return;
            }

            await Task.Delay(duration, currentRequest.Cancellation.Token);

            lock (_gate)
            {
                if (_disposed != 0 ||
                    currentRequest.Cancellation.IsCancellationRequested ||
                    !ReferenceEquals(_currentRequest, currentRequest))
                {
                    return;
                }
            }

            await onElapsed();
        }
        catch (OperationCanceledException)
        {
            // A newer overlay request or disposal cancelled this timer.
        }
        finally
        {
            var disposeCurrent = false;
            lock (_gate)
            {
                if (ReferenceEquals(_currentRequest, currentRequest))
                {
                    _currentRequest = null;
                    disposeCurrent = true;
                }
                else if (!currentRequest.IsSuperseded && _disposed != 0)
                {
                    // Dispose removed this request from the coordinator; the
                    // active request still owns its source.
                    disposeCurrent = true;
                }
            }

            if (disposeCurrent)
                currentRequest.Cancellation.Dispose();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        DismissalRequest? currentRequest;
        lock (_gate)
        {
            _disposed = 1;
            currentRequest = _currentRequest;
            _currentRequest = null;
        }

        currentRequest?.Cancellation.Cancel();
        // The active RunAsync call owns disposal of its source in finally.
        GC.SuppressFinalize(this);
    }

    private sealed class DismissalRequest
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public bool IsSuperseded { get; set; }
    }
}
