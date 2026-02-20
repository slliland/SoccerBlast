using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Api.Services.Video;

// Load .env from project directory (THESPORTSDB_API_KEY)
var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var s = line.Trim();
        if (s.Length == 0 || s[0] == '#') continue;
        var eq = s.IndexOf('=');
        if (eq <= 0) continue;
        var key = s[0..eq].Trim();
        var value = eq < s.Length - 1 ? s[(eq + 1)..].Trim() : "";
        if (value.StartsWith('"') && value.EndsWith('"')) value = value[1..^1].Replace("\\\"", "\"");
        Environment.SetEnvironmentVariable(key, value);
    }
}

var builder = WebApplication.CreateBuilder(args);
if (Environment.GetEnvironmentVariable("THESPORTSDB_API_KEY") is { } sportsDbKey)
    builder.Configuration["TheSportsDb:ApiKey"] = sportsDbKey;

// Core
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core (SQLite)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cache (once)
builder.Services.AddMemoryCache();

//
// Options
//
builder.Services.Configure<TheSportsDbOptions>(
    builder.Configuration.GetSection("TheSportsDb"));

builder.Services.Configure<YouTubeSourceOptions>(
    builder.Configuration.GetSection("VideoSources:YouTube"));

//
// Typed HttpClients
//

// TheSportsDB v2 (team/league/players) – X-API-KEY header
builder.Services.AddHttpClient("sportsdb", (sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<TheSportsDbOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
    client.DefaultRequestHeaders.Add("X-API-KEY", opt.ApiKey.Trim());
});
// TheSportsDB v1 (eventsday only – v2 has no events-by-day endpoint)
builder.Services.AddHttpClient("sportsdb-v1", (sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<TheSportsDbOptions>>().Value;
    client.BaseAddress = new Uri($"{opt.BaseUrlV1.TrimEnd('/')}/{opt.ApiKey.Trim()}/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});
builder.Services.AddSingleton<TheSportsDbClient>();
builder.Services.AddSingleton<SportsDbMatchesClient>();

// News (if your NewsService uses HttpClient, this is enough)
builder.Services.AddHttpClient<NewsService>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});

//
// App services
//
builder.Services.AddScoped<MatchSyncService>();
builder.Services.AddScoped<TeamProfileSyncService>();

// Video
builder.Services.AddHttpClient("youtube-rss", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});

// If these don’t depend on scoped services/db, singleton is OK
builder.Services.AddSingleton<YouTubeRssClient>();
builder.Services.AddSingleton<VideoAggregator>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
