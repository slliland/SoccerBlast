using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;
using SoccerBlast.Shared.Contracts;

namespace SoccerBlast.Web.Services;

public sealed class UserPrefsStore
{
    private const string Key = "sb.prefs.v1";
    private readonly IJSRuntime _js;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public UserPrefsStore(IJSRuntime js) => _js = js;

    public async Task<UserPrefs> GetAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("sbPrefs.get", Key);
            if (string.IsNullOrWhiteSpace(json))
            {
                Console.WriteLine("[prefs] loaded: <empty> -> defaults");
                return new UserPrefs();
            }

            var prefs = JsonSerializer.Deserialize<UserPrefs>(json, JsonOpts) ?? new UserPrefs();

            prefs.FollowedTeamIds ??= new();
            prefs.PinnedCompetitionIds ??= new();
            prefs.FollowedTeamIds = prefs.FollowedTeamIds.Distinct().ToList();
            prefs.PinnedCompetitionIds = prefs.PinnedCompetitionIds.Distinct().ToList();

            prefs.DefaultViewMode =
                prefs.DefaultViewMode?.Equals("cards", StringComparison.OrdinalIgnoreCase) == true ? "Cards" :
                prefs.DefaultViewMode?.Equals("timeline", StringComparison.OrdinalIgnoreCase) == true ? "Timeline" :
                (string.IsNullOrWhiteSpace(prefs.DefaultViewMode) ? "Cards" : prefs.DefaultViewMode);

            Console.WriteLine($"[prefs] loaded: FollowedTeamsOnly={prefs.FollowedTeamsOnly}, FollowedTeamIds={prefs.FollowedTeamIds.Count}, ViewMode={prefs.DefaultViewMode}");
            return prefs;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[prefs] load failed: {ex.Message}");
            return new UserPrefs();
        }
    }

    public async Task SaveAsync(UserPrefs prefs)
    {
        prefs.FollowedTeamIds ??= new();
        prefs.PinnedCompetitionIds ??= new();

        prefs.DefaultViewMode =
            prefs.DefaultViewMode?.Equals("cards", StringComparison.OrdinalIgnoreCase) == true ? "Cards" :
            prefs.DefaultViewMode?.Equals("timeline", StringComparison.OrdinalIgnoreCase) == true ? "Timeline" :
            prefs.DefaultViewMode;

        var json = JsonSerializer.Serialize(prefs, JsonOpts);

        Console.WriteLine($"[prefs] saving: FollowedTeamsOnly={prefs.FollowedTeamsOnly}, FollowedTeamIds={prefs.FollowedTeamIds.Count}, ViewMode={prefs.DefaultViewMode}");
        await _js.InvokeVoidAsync("sbPrefs.set", Key, json);
    }
}
