namespace SoccerBlast.Shared.Contracts;

/// <summary>Core match/event from TheSportsDB lookup/event/{idEvent}.</summary>
public class MatchDetailDto
{
    public string IdEvent { get; set; } = "";
    public string? StrEvent { get; set; }
    public string? StrFilename { get; set; }
    public string? StrSport { get; set; }
    public string? IdLeague { get; set; }
    public string? StrLeague { get; set; }
    public string? StrLeagueBadge { get; set; }
    public string? StrSeason { get; set; }
    public string? StrRound { get; set; }

    public string? IdHomeTeam { get; set; }
    public string? IdAwayTeam { get; set; }
    public string? StrHomeTeam { get; set; }
    public string? StrAwayTeam { get; set; }
    public string? StrHomeTeamBadge { get; set; }
    public string? StrAwayTeamBadge { get; set; }

    public string? DateEvent { get; set; }
    public string? StrTime { get; set; }
    public DateTime? UtcDate { get; set; }

    public int? IntHomeScore { get; set; }
    public int? IntAwayScore { get; set; }
    public string? StrStatus { get; set; }
    public string? StrProgress { get; set; }
    public string? StrResult { get; set; }

    public string? StrVenue { get; set; }
    public string? IdVenue { get; set; }
    public string? StrCountry { get; set; }

    public string? StrThumb { get; set; }
    public string? StrBanner { get; set; }
    public string? StrPoster { get; set; }
    public string? StrFanart { get; set; }
}

/// <summary>One player in event lineup (home or away).</summary>
public class EventLineupPlayerDto
{
    public string? IdPlayer { get; set; }
    public string? StrPlayer { get; set; }
    public string? StrPosition { get; set; }
    public string? StrNumber { get; set; }
    public string? StrGrid { get; set; }
    public string? StrRole { get; set; }  // StartXI, Substitute, etc.
    public string? IdTeam { get; set; }
    public string? StrTeam { get; set; }
    public string? StrThumb { get; set; }
    public string? StrCutout { get; set; }
    public string? StrNationality { get; set; }
}

/// <summary>Lineup for one team (home or away).</summary>
public class EventLineupTeamDto
{
    public string? IdTeam { get; set; }
    public string? StrTeam { get; set; }
    public string? StrTeamBadge { get; set; }
    public List<EventLineupPlayerDto> Players { get; set; } = new();
}

/// <summary>Full lineup: home + away.</summary>
public class EventLineupDto
{
    public EventLineupTeamDto? Home { get; set; }
    public EventLineupTeamDto? Away { get; set; }
}

/// <summary>One timeline item (goal, card, substitution).</summary>
public class EventTimelineItemDto
{
    public string? StrTime { get; set; }   // minute, e.g. "11"
    public string? StrTimeline { get; set; } // type: Goal, Card, Substitution, etc.
    public string? StrPlayer { get; set; }
    public string? StrTeam { get; set; }
    public string? IdPlayer { get; set; }
    public string? IdTeam { get; set; }
    public string? StrDetail { get; set; }
    public string? StrEvent { get; set; }
}

/// <summary>One stat row (e.g. Possession 60% - 40%).</summary>
public class EventStatRowDto
{
    public string? StrStat { get; set; }
    public int? IntHome { get; set; }
    public int? IntAway { get; set; }
}

/// <summary>One highlight (YouTube + thumb).</summary>
public class EventHighlightDto
{
    public string? StrVideo { get; set; }
    public string? StrThumb { get; set; }
    public string? StrTitle { get; set; }
}

/// <summary>One TV entry (channel / region).</summary>
public class EventTvDto
{
    public string? StrChannel { get; set; }
    public string? StrCountry { get; set; }
}

/// <summary>Composite response for match page: event + lineup + timeline + stats + highlights + tv.</summary>
public class MatchDetailResponseDto
{
    public MatchDetailDto Event { get; set; } = new();
    public EventLineupDto? Lineup { get; set; }
    public List<EventTimelineItemDto> Timeline { get; set; } = new();
    public List<EventStatRowDto> Stats { get; set; } = new();
    public List<EventHighlightDto> Highlights { get; set; } = new();
    public List<EventTvDto> Tv { get; set; } = new();
}
