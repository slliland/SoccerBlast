using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

public sealed class TeamProfileSyncService
{
    public const string Provider = "SportsDB";

    private const int MaxResolverCalls = 4;
    private static readonly TimeSpan ResolverTimeBudget = TimeSpan.FromSeconds(8);

    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;
    private readonly ILogger<TeamProfileSyncService> _log;

    public TeamProfileSyncService(AppDbContext db, TheSportsDbClient sportsDb, ILogger<TeamProfileSyncService> log)
    {
        _db = db;
        _sportsDb = sportsDb;
        _log = log;
    }

    public async Task<(bool ok, string message)> SyncTeamProfileAsync(int teamId, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team == null) return (false, $"Team {teamId} not found.");

        var profile = await _db.TeamProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.TeamId == teamId, ct);
        var map = await _db.TeamExternalMaps
            .FirstOrDefaultAsync(x => x.TeamId == teamId && x.Provider == Provider, ct);

        sw.Stop();
        _log.LogInformation("Team query: {Ms}ms", sw.ElapsedMilliseconds);

        SportsDbTeam? sportsTeam = null;

        sw.Restart();
        if (map != null && !string.IsNullOrWhiteSpace(map.ExternalId))
        {
            sportsTeam = await _sportsDb.LookupTeamAsync(map.ExternalId, ct);
            _log.LogInformation("SportsDB lookupteam id={ExternalId} took {Ms}ms", map.ExternalId, sw.ElapsedMilliseconds);
        }

        if (sportsTeam == null)
        {
            sportsTeam = await ResolveTeamWithGuardrailsAsync(team, profile, ct);
            _log.LogInformation("SportsDB resolver for '{Name}' took {Ms}ms", team.Name, sw.ElapsedMilliseconds);
        }

        sw.Stop();
        _log.LogInformation("SportsDB fetch total: {Ms}ms", sw.ElapsedMilliseconds);

        if (sportsTeam == null || string.IsNullOrWhiteSpace(sportsTeam.IdTeam))
            return (false, $"No SportsDB match for '{team.Name}'.");

        // 3) ensure mapping exists and is updated (unique on Provider + ExternalId: one SportsDB id → one team)
        if (map == null)
        {
            var existingWithSameExternalId = await _db.TeamExternalMaps
                .FirstOrDefaultAsync(x => x.Provider == Provider && x.ExternalId == sportsTeam.IdTeam, ct);
            if (existingWithSameExternalId == null)
            {
                map = new TeamExternalMap
                {
                    TeamId = teamId,
                    Provider = Provider,
                    ExternalId = sportsTeam.IdTeam!,
                    LastSyncedUtc = DateTime.UtcNow
                };
                _db.TeamExternalMaps.Add(map);
            }
            // else: another team already has this ExternalId; skip adding map, still save profile below
        }
        else
        {
            map.ExternalId = sportsTeam.IdTeam!;
            map.LastSyncedUtc = DateTime.UtcNow;
        }

        // 4) upsert profile (tracked for save)
        var profileToSave = await _db.TeamProfiles.FirstOrDefaultAsync(p => p.TeamId == teamId, ct);
        if (profileToSave == null)
        {
            profileToSave = new TeamProfile { TeamId = teamId };
            _db.TeamProfiles.Add(profileToSave);
        }

        // Basic Info
        profileToSave.FormedYear = TryParseInt(sportsTeam.IntFormedYear);
        profileToSave.Location = NullIfBlank(sportsTeam.StrLocation);
        profileToSave.Keywords = NullIfBlank(sportsTeam.StrKeywords);

        // Stadium
        profileToSave.StadiumName = NullIfBlank(sportsTeam.StrStadium);
        profileToSave.StadiumLocation = NullIfBlank(sportsTeam.StrStadiumLocation);
        profileToSave.StadiumCapacity = TryParseInt(sportsTeam.IntStadiumCapacity);

        // Leagues - combine all non-null leagues into comma-separated list
        var leagues = new[] 
        { 
            sportsTeam.StrLeague, 
            sportsTeam.StrLeague2, 
            sportsTeam.StrLeague3, 
            sportsTeam.StrLeague4, 
            sportsTeam.StrLeague5, 
            sportsTeam.StrLeague6, 
            sportsTeam.StrLeague7 
        }
        .Where(l => !string.IsNullOrWhiteSpace(l))
        .Select(l => l!.Trim())
        .ToList();
        
        profileToSave.Leagues = leagues.Count > 0 ? string.Join(", ", leagues) : null;

        // Description
        profileToSave.DescriptionEn = NullIfBlank(sportsTeam.StrDescriptionEN);

        // Media
        profileToSave.BannerUrl = NullIfBlank(sportsTeam.StrBanner);
        profileToSave.JerseyUrl = NullIfBlank(sportsTeam.StrEquipment);
        profileToSave.BadgeUrl = NullIfBlank(sportsTeam.StrBadge);
        profileToSave.LogoUrl = NullIfBlank(sportsTeam.StrLogo);

        // Colors
        profileToSave.PrimaryColor = NullIfBlank(sportsTeam.StrColour1);
        profileToSave.SecondaryColor = NullIfBlank(sportsTeam.StrColour2);
        profileToSave.TertiaryColor = NullIfBlank(sportsTeam.StrColour3);

        // Social Media (clean URLs)
        profileToSave.Website = CleanUrl(sportsTeam.StrWebsite);
        profileToSave.Facebook = CleanUrl(sportsTeam.StrFacebook);
        profileToSave.Twitter = CleanUrl(sportsTeam.StrTwitter);
        profileToSave.Instagram = CleanUrl(sportsTeam.StrInstagram);
        profileToSave.Youtube = CleanUrl(sportsTeam.StrYoutube);

        profileToSave.LastUpdatedUtc = DateTime.UtcNow;

        sw.Restart();
        await _db.SaveChangesAsync(ct);
        sw.Stop();
        _log.LogInformation("SaveChanges: {Ms}ms", sw.ElapsedMilliseconds);

        return (true, $"Synced SportsDB profile for '{team.Name}'.");
    }

    /// <summary>
    /// Multi-step resolver: max 4 SportsDB calls, ~8s budget. Step A: search with normalized name.
    /// If 1 match → use it. If multiple → disambiguate (stadium, formed year). If 0 → try up to 2–3 curated aliases.
    /// </summary>
    private async Task<SportsDbTeam?> ResolveTeamWithGuardrailsAsync(Team team, TeamProfile? existingProfile, CancellationToken ct)
    {
        using var timeoutCts = new CancellationTokenSource(ResolverTimeBudget);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linked.Token;

        var searchTerms = new List<string>();
        var normalized = TeamNameNormalizer.Normalize(team.Name);
        if (!string.IsNullOrEmpty(normalized) && normalized.Length >= 2)
            searchTerms.Add(normalized);
        foreach (var alias in TeamNameNormalizer.GetCuratedAliases(team.Name))
        {
            if (!string.IsNullOrWhiteSpace(alias) && !searchTerms.Contains(alias, StringComparer.OrdinalIgnoreCase))
                searchTerms.Add(alias.Trim());
            if (searchTerms.Count >= 4) break;
        }

        int callsUsed = 0;
        foreach (var term in searchTerms)
        {
            if (callsUsed >= MaxResolverCalls) break;
            token.ThrowIfCancellationRequested();

            var list = await _sportsDb.SearchTeamsAsync(term, token);
            callsUsed++;

            if (list.Count == 0) continue;
            if (list.Count == 1) return list[0];
            var best = Disambiguate(list, team, existingProfile);
            if (best != null) return best;
        }

        return null;
    }

    private static SportsDbTeam? Disambiguate(List<SportsDbTeam> candidates, Team team, TeamProfile? profile)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        var ourStadium = profile?.StadiumName?.Trim();
        var ourFormed = profile?.FormedYear;
        var ourName = team.Name.Trim();

        int Score(SportsDbTeam c)
        {
            int s = 0;
            var theirStadium = (c.StrStadium ?? "").Trim();
            if (!string.IsNullOrEmpty(ourStadium) && !string.IsNullOrEmpty(theirStadium))
            {
                if (string.Equals(ourStadium, theirStadium, StringComparison.OrdinalIgnoreCase)) s += 2;
                else if (theirStadium.Contains(ourStadium, StringComparison.OrdinalIgnoreCase) || ourStadium.Contains(theirStadium, StringComparison.OrdinalIgnoreCase)) s += 1;
            }
            if (ourFormed.HasValue && int.TryParse((c.IntFormedYear ?? "").Trim(), out var fy))
            {
                if (fy == ourFormed.Value) s += 2;
                else if (Math.Abs(fy - ourFormed.Value) <= 2) s += 1;
            }
            var theirName = (c.StrTeam ?? "").Trim();
            if (ourName.Contains(theirName, StringComparison.OrdinalIgnoreCase) || theirName.Contains(ourName, StringComparison.OrdinalIgnoreCase))
                s += 1;
            return s;
        }

        var best = candidates.MaxBy(Score)!;
        return best;
    }

    private static int? TryParseInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var v) ? v : null;

    private static string? NullIfBlank(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? CleanUrl(string? url)
    {
        var cleaned = NullIfBlank(url);
        if (cleaned == null) return null;
        
        // Add https:// if missing
        if (!cleaned.StartsWith("http://") && !cleaned.StartsWith("https://"))
        {
            cleaned = "https://" + cleaned;
        }
        
        return cleaned;
    }
}
