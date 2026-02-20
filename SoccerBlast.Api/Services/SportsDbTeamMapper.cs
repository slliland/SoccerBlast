using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Api.Services;

/// <summary>Maps TheSportsDB v2 team response (SportsDbTeam) to TeamProfileDto for API responses.</summary>
public static class SportsDbTeamMapper
{
    public static TeamProfileDto ToTeamProfileDto(SportsDbTeam t, int teamId)
    {
        var leagues = new[]
        {
            t.StrLeague, t.StrLeague2, t.StrLeague3, t.StrLeague4,
            t.StrLeague5, t.StrLeague6, t.StrLeague7
        }
        .Where(l => !string.IsNullOrWhiteSpace(l))
        .Select(l => l!.Trim())
        .ToList();

        return new TeamProfileDto
        {
            TeamId = teamId,
            FormedYear = TryParseInt(t.IntFormedYear),
            Location = NullIfBlank(t.StrLocation),
            Keywords = NullIfBlank(t.StrKeywords),
            StadiumName = NullIfBlank(t.StrStadium),
            StadiumCapacity = TryParseInt(t.IntStadiumCapacity),
            StadiumLocation = NullIfBlank(t.StrStadiumLocation),
            Leagues = leagues.Count > 0 ? string.Join(", ", leagues) : null,
            LeaguesWithBadges = leagues.Count > 0 ? leagues.Select(n => new LeagueEntryDto(n, null)).ToList() : null,
            DescriptionEn = NullIfBlank(t.StrDescriptionEN),
            BannerUrl = NullIfBlank(t.StrBanner),
            FanartUrls = new[] { t.StrFanart1, t.StrFanart2, t.StrFanart3, t.StrFanart4 }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList(),
            JerseyUrl = NullIfBlank(t.StrEquipment),
            BadgeUrl = NullIfBlank(t.StrBadge),
            LogoUrl = NullIfBlank(t.StrLogo),
            PrimaryColor = NullIfBlank(t.StrColour1),
            SecondaryColor = NullIfBlank(t.StrColour2),
            TertiaryColor = NullIfBlank(t.StrColour3),
            Website = CleanUrl(t.StrWebsite),
            Facebook = CleanUrl(t.StrFacebook),
            Twitter = CleanUrl(t.StrTwitter),
            Instagram = CleanUrl(t.StrInstagram),
            Youtube = CleanUrl(t.StrYoutube),
            LastUpdatedUtc = null
        };
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
