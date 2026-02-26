using Microsoft.Extensions.Caching.Memory;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Services;

/// <summary>Builds team roster from list/players and enriches each player via lookup/player (cached).</summary>
public class PlayerRosterService
{
    private const string CacheKeyPrefix = "player_lookup:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly TheSportsDbClient _sportsDb;
    private readonly IMemoryCache _cache;

    public PlayerRosterService(TheSportsDbClient sportsDb, IMemoryCache cache)
    {
        _sportsDb = sportsDb;
        _cache = cache;
    }

    public async Task<List<TeamPlayerDto>> GetEnrichedRosterAsync(string sportsDbTeamId, CancellationToken ct = default)
    {
        var list = await _sportsDb.GetTeamPlayersAsync(sportsDbTeamId, ct);
        var result = new List<TeamPlayerDto>(list.Count);
        foreach (var p in list)
        {
            var enriched = await EnrichAsync(p, ct);
            result.Add(enriched);
        }
        return result;
    }

    private async Task<TeamPlayerDto> EnrichAsync(TeamPlayerDto basePlayer, CancellationToken ct)
    {
        var id = basePlayer.SportsDbPlayerId;
        if (string.IsNullOrWhiteSpace(id))
            return basePlayer;

        var lookup = await _cache.GetOrCreateAsync(CacheKeyPrefix + id, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _sportsDb.LookupPlayerAsync(id, ct);
        });

        if (lookup == null)
            return basePlayer;

        return Merge(basePlayer, lookup);
    }

    private static TeamPlayerDto Merge(TeamPlayerDto basePlayer, SportsDbPlayerLookup lookup)
    {
        return new TeamPlayerDto
        {
            SportsDbPlayerId = basePlayer.SportsDbPlayerId,
            Name = basePlayer.Name,
            Position = lookup.StrPosition ?? basePlayer.Position,
            Nationality = lookup.StrNationality ?? basePlayer.Nationality,
            JerseyNumber = lookup.StrNumber,
            ThumbUrl = lookup.StrThumb ?? basePlayer.ThumbUrl,
            CutoutUrl = lookup.StrCutout ?? basePlayer.CutoutUrl,
            RenderUrl = lookup.StrRender ?? basePlayer.RenderUrl,
            CartoonUrl = lookup.StrCartoon,
            Age = ParseAge(lookup.DateBorn),
            Height = lookup.StrHeight,
            Weight = lookup.StrWeight,
            Wage = lookup.StrWage,
            DateBorn = lookup.DateBorn,
            StrSigning = lookup.StrSigning,
        };
    }

    private static int? ParseAge(string? dateBorn)
    {
        if (string.IsNullOrWhiteSpace(dateBorn)) return null;
        if (!DateTime.TryParse(dateBorn.Trim(), out var dob)) return null;
        var today = DateTime.UtcNow.Date;
        var age = today.Year - dob.Year;
        if (dob.Date > today.AddYears(-age)) age--;
        return age >= 0 && age <= 120 ? age : null;
    }
}
