using LoupixDeck.PluginSdk;

namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>
/// Keeps a cached <see cref="HeadsetStatus"/> up to date by reading the headset on its own
/// background loop. Render and display callbacks only ever read the cache, so they stay fast.
/// </summary>
internal sealed class HeadsetMonitor : IDisposable
{
    private readonly IPluginLogger _logger;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _readGate = new(1, 1);

    private CancellationTokenSource? _cts;
    private string _executable;
    private TimeSpan _interval;
    private HeadsetStatus _status = HeadsetStatus.Unknown;
    private bool _reportedUnavailable;

    public HeadsetMonitor(IPluginLogger logger, string executable, TimeSpan interval)
    {
        _logger = logger;
        _executable = executable;
        _interval = interval;
    }

    /// <summary>Raised when a read produced a different status. May run on a background thread.</summary>
    public event Action<HeadsetStatus>? StatusChanged;

    public HeadsetStatus Status
    {
        get
        {
            lock (_gate)
                return _status;
        }
    }

    /// <summary>Starts (or restarts) the polling loop. Reads the headset immediately.</summary>
    public void Start()
    {
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _cts;
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;
            _ = Task.Run(() => RunAsync(token), CancellationToken.None);
        }

        if (previous is null)
            return;

        // Cancel only. Disposing the source here races with a loop that is registering the
        // cancellation callback for its next delay; the source holds no unmanaged timer.
        previous.Cancel();
    }

    /// <summary>Applies new settings and restarts the loop.</summary>
    public void Restart(string executable, TimeSpan interval)
    {
        lock (_gate)
        {
            _executable = executable;
            _interval = interval;
        }

        Start();
    }

    /// <summary>Reads the headset now, outside the poll cadence. Never throws.</summary>
    public async Task<HeadsetStatus> RefreshNowAsync()
    {
        CancellationToken token;
        lock (_gate)
            token = _cts?.Token ?? CancellationToken.None;

        try
        {
            return await ReadAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Status;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Reading the headset failed: {ex.Message}");
            return Status;
        }
    }

    /// <summary>Stops the loop. A read in progress is cancelled and its process killed.</summary>
    public void Dispose()
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
        }

        if (cts is null)
            return;

        cts.Cancel();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Reading the headset failed: {ex.Message}");
            }

            TimeSpan interval;
            lock (_gate)
                interval = _interval;

            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<HeadsetStatus> ReadAsync(CancellationToken cancellationToken)
    {
        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string executable;
            lock (_gate)
                executable = _executable;

            HeadsetStatus status =
                await new HyperHeadsetClient(executable).ReadAsync(cancellationToken).ConfigureAwait(false);
            return UpdateStatus(status);
        }
        finally
        {
            _readGate.Release();
        }
    }

    private HeadsetStatus UpdateStatus(HeadsetStatus status)
    {
        bool changed;
        bool logConnected = false;
        bool logUnavailable = false;

        lock (_gate)
        {
            // Charging status is event-driven in the CLI and is occasionally absent from a
            // read even though the headset is connected. Keep the last known state instead of
            // blanking the caption until the next poll.
            if (status.Connected
                && status.Charging == ChargingState.Unknown
                && _status.Connected)
            {
                status = status with { Charging = _status.Charging };
            }

            changed = _status != status;
            _status = status;

            if (status.Connected && _reportedUnavailable)
            {
                _reportedUnavailable = false;
                logConnected = true;
            }
            else if (!status.Connected && !_reportedUnavailable)
            {
                _reportedUnavailable = true;
                logUnavailable = true;
            }
        }

        if (logConnected)
            _logger.Info("HyperX headset is available.");
        if (logUnavailable)
            _logger.Warn(status.Error ?? "HyperX headset is not available.");

        if (!changed)
            return status;

        try
        {
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            _logger.Warn($"A headset status handler failed: {ex.Message}");
        }

        return status;
    }
}
