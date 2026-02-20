using System.Threading;

namespace SoccerBlast.Web.Services;

public sealed class AutoSyncLoop : IDisposable
{
    private readonly TimeSpan _interval;
    private readonly Func<CancellationToken, Task> _onTick;
    private readonly Action<Exception>? _onError;

    private PeriodicTimer? _timer;
    private CancellationTokenSource? _cts;

    private volatile int _running; // 0/1 guard
    private bool _started;

    public AutoSyncLoop(TimeSpan interval, Func<CancellationToken, Task> onTick, Action<Exception>? onError = null)
    {
        _interval = interval;
        _onTick = onTick;
        _onError = onError;
    }

    public bool IsRunning => Volatile.Read(ref _started);

    public void Start()
    {
        if (Volatile.Read(ref _started)) return;
        Volatile.Write(ref _started, true);

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _timer?.Dispose();
        _timer = new PeriodicTimer(_interval);

        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        Volatile.Write(ref _started, false);
        try { _cts?.Cancel(); } catch { }
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var timer = _timer;
                if (timer is null) break;

                var ok = await timer.WaitForNextTickAsync(token);
                if (!ok) break;
                if (!Volatile.Read(ref _started)) continue;

                if (Interlocked.Exchange(ref _running, 1) == 1)
                    continue;

                try
                {
                    await _onTick(token);
                }
                catch (Exception ex)
                {
                    _onError?.Invoke(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _running, 0);
                }
            }
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
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
        catch { }
    }
}
