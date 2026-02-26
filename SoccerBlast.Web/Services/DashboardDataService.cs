using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services;

public sealed class DashboardDataService
{
    private readonly SoccerApiClient _api;

    public DashboardDataService(SoccerApiClient api)
    {
        _api = api;
    }

    // Fast load from DB first (for instant UI), then optional sync refresh
    public async Task<DashboardLoadResult> LoadMatchesForDateAsync(
        DateOnly date,
        bool trySyncAfterLoad = true,
        CancellationToken ct = default)
    {
        // 1) quick local DB load
        var initial = await _api.GetByLocalDateAsync(date);

        int synced = 0;
        List<MatchDto>? refreshed = null;
        string? warning = null;

        // 2) sync and refresh (optional)
        if (trySyncAfterLoad)
        {
            try
            {
                synced = await _api.SyncByLocalDateAsync(date);

                // Always re-fetch after sync attempt (not only synced > 0),
                // because scores/status may change without count changing
                refreshed = await _api.GetByLocalDateAsync(date);
            }
            catch (Exception ex)
            {
                warning = ex.Message;
            }
        }

        return new DashboardLoadResult(
            InitialMatches: initial ?? new(),
            RefreshedMatches: refreshed,
            SyncedCount: synced,
            Warning: warning
        );
    }

    public async Task<List<NewsDto>> LoadNewsAsync(int limit = 10, CancellationToken ct = default)
    {
        return await _api.GetRecentNewsAsync(limit) ?? new();
    }
}

public sealed record DashboardLoadResult(
    List<MatchDto> InitialMatches,
    List<MatchDto>? RefreshedMatches,
    int SyncedCount,
    string? Warning
);

