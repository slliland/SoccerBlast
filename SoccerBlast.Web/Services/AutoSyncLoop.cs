using System.Threading;

namespace SoccerBlast.Web.Services;

/// <summary>
/// Small reusable periodic loop runner for pages.
/// </summary>
public sealed class AutoSyncLoop : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Func<CancellationToken, Task> _onTick;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    private volatile int _running; // 0/1 guard
    private bool _started;

    public AutoSyncLoop(TimeSpan interval, Func<CancellationToken, Task> onTick)
    {
        _interval = interval;
        _onTick = onTick;
    }

    public bool IsRunning => _started;

    public void Start()
    {
        if (_started) return;
        _started = true;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _timer?.Dispose();
        _timer = new PeriodicTimer(_interval);

        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        _started = false;
        try
        {
            _cts?.Cancel();
        }
        catch { /* ignore */ }
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _timer != null)
            {
                var ok = await _timer.WaitForNextTickAsync(token);
                if (!ok) break;
                if (!_started) continue;

                // no overlap
                if (Interlocked.Exchange(ref _running, 1) == 1)
                    continue;

                try
                {
                    await _onTick(token);
                }
                catch
                {
                    // swallow errors so the loop keeps running
                }
                finally
                {
                    Interlocked.Exchange(ref _running, 0);
                }
            }
        }
        catch
        {
            // ignore timer/cancel exceptions
        }
    }

    public void Dispose()
    {
        Stop();
        try
        {
            _timer?.Dispose();
            _cts?.Dispose();
        }
        catch { /* ignore */ }
    }
}
