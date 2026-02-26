using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Models;

namespace SoccerBlast.Api.Services;

/// <summary>
/// Syncs team profile from SportsDB only. Single source: team identity is SportsDB id (Team.SportsDbId or Team.Id).
/// </summary>
public sealed class TeamProfileSyncService
{
    private readonly AppDbContext _db;
    private readonly TheSportsDbClient _sportsDb;
    private readonly ILogger<TeamProfileSyncService> _log;

    public TeamProfileSyncService(AppDbContext db, TheSportsDbClient sportsDb, ILogger<TeamProfileSyncService> log)
    {
        _db = db;
        _sportsDb = sportsDb;
        _log = log;
    }

    /// <summary>Fetch profile from SportsDB by team's SportsDB id and save to TeamProfile. No resolver, no mapping table.</summary>
    public async Task<(bool ok, string message)> SyncTeamProfileAsync(int teamId, CancellationToken ct = default)
    {
        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team == null) return (false, $"Team {teamId} not found.");

        var sportsDbId = !string.IsNullOrWhiteSpace(team.SportsDbId) ? team.SportsDbId!.Trim() : teamId.ToString();
        var sw = Stopwatch.StartNew();
        var sportsTeam = await _sportsDb.LookupTeamAsync(sportsDbId, ct);
        sw.Stop();
        _log.LogInformation("SportsDB lookup team {SportsDbId} took {Ms}ms", sportsDbId, sw.ElapsedMilliseconds);

        if (sportsTeam == null || string.IsNullOrWhiteSpace(sportsTeam.IdTeam))
            return (false, $"No SportsDB team for id '{sportsDbId}'.");

        var profileToSave = await _db.TeamProfiles.FirstOrDefaultAsync(p => p.TeamId == teamId, ct);
        if (profileToSave == null)
        {
            profileToSave = new TeamProfile { TeamId = teamId };
            _db.TeamProfiles.Add(profileToSave);
        }

        profileToSave.FormedYear = TryParseInt(sportsTeam.IntFormedYear);
        profileToSave.Location = NullIfBlank(sportsTeam.StrLocation);
        profileToSave.Keywords = NullIfBlank(sportsTeam.StrKeywords);
        profileToSave.StadiumName = NullIfBlank(sportsTeam.StrStadium);
        profileToSave.StadiumLocation = NullIfBlank(sportsTeam.StrStadiumLocation);
        profileToSave.StadiumCapacity = TryParseInt(sportsTeam.IntStadiumCapacity);

        var leagues = new[]
        {
            sportsTeam.StrLeague, sportsTeam.StrLeague2, sportsTeam.StrLeague3, sportsTeam.StrLeague4,
            sportsTeam.StrLeague5, sportsTeam.StrLeague6, sportsTeam.StrLeague7
        }.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!.Trim()).ToList();
        profileToSave.Leagues = leagues.Count > 0 ? string.Join(", ", leagues) : null;

        profileToSave.DescriptionEn = NullIfBlank(sportsTeam.StrDescriptionEN);
        profileToSave.BannerUrl = NullIfBlank(sportsTeam.StrBanner);
        profileToSave.JerseyUrl = NullIfBlank(sportsTeam.StrEquipment);
        profileToSave.BadgeUrl = NullIfBlank(sportsTeam.StrBadge);
        profileToSave.LogoUrl = NullIfBlank(sportsTeam.StrLogo);
        profileToSave.PrimaryColor = NullIfBlank(sportsTeam.StrColour1);
        profileToSave.SecondaryColor = NullIfBlank(sportsTeam.StrColour2);
        profileToSave.TertiaryColor = NullIfBlank(sportsTeam.StrColour3);
        profileToSave.Website = CleanUrl(sportsTeam.StrWebsite);
        profileToSave.Facebook = CleanUrl(sportsTeam.StrFacebook);
        profileToSave.Twitter = CleanUrl(sportsTeam.StrTwitter);
        profileToSave.Instagram = CleanUrl(sportsTeam.StrInstagram);
        profileToSave.Youtube = CleanUrl(sportsTeam.StrYoutube);
        profileToSave.LastUpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (true, $"Synced SportsDB profile for '{team.Name}'.");
    }

    private static int? TryParseInt(string? s)
        => int.TryParse((s ?? "").Trim(), out var v) ? v : null;

    private static string? NullIfBlank(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? CleanUrl(string? url)
    {
        var cleaned = NullIfBlank(url);
        if (cleaned == null) return null;
        if (!cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            cleaned = "https://" + cleaned;
        return cleaned;
    }
}
