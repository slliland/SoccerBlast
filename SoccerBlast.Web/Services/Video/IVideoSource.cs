using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services.Video;

public interface IVideoSource
{
    Task<IReadOnlyList<VideoDto>> GetRecentAsync(
        int limit = 200,
        int? competitionId = null,
        string? tag = null,
        string? q = null,
        string? source = null,
        DateTimeOffset? sinceUtc = null,
        CancellationToken ct = default);
}
