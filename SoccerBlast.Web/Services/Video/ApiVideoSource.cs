using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services.Video;

public sealed class ApiVideoSource : IVideoSource
{
    private readonly HttpClient _http;
    public ApiVideoSource(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<VideoDto>> GetRecentAsync(
        int limit = 200,
        int? competitionId = null,
        string? tag = null,
        string? q = null,
        string? source = null,
        DateTimeOffset? sinceUtc = null,
        CancellationToken ct = default)
    {
        var qs = new Dictionary<string, string?>
        {
            ["limit"] = limit.ToString(),
            ["competitionId"] = competitionId?.ToString(),
            ["tag"] = string.IsNullOrWhiteSpace(tag) ? null : tag,
            ["q"] = string.IsNullOrWhiteSpace(q) ? null : q,
            ["source"] = string.IsNullOrWhiteSpace(source) ? null : source,
            ["sinceUtc"] = sinceUtc?.ToString("O")
        };

        var url = QueryHelpers.AddQueryString("api/videos/recent", qs!);
        var list = await _http.GetFromJsonAsync<List<VideoDto>>(url, ct);
        return list ?? [];
    }
}
