using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Api.Services.Video;
using SoccerBlast.Api.Services.Search;

// Load .env from project directory (THESPORTSDB_API_KEY)
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (!File.Exists(envPath))
{
    // fallback: project root when running from bin/Debug/netX
    var baseDir = AppContext.BaseDirectory;
    var dir = new DirectoryInfo(baseDir);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SoccerBlast.Api.csproj")))
        dir = dir.Parent;

    if (dir != null)
        envPath = Path.Combine(dir.FullName, ".env");
}
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

// EF Core (Postgres / Supabase)
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(conn))
    conn = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(conn))
    throw new InvalidOperationException("Missing Postgres connection string. Set ConnectionStrings:DefaultConnection or SUPABASE_CONNECTION_STRING.");
// Use Supabase Session pooler (port 5432) for EF Core — connection held for the whole session, so no
// "random hang at DB call" from transaction-mode pooler. Transaction mode (port 6543) returns the
// connection after each transaction and is meant for serverless; session mode is best for ORMs.
// Multiplexing=false keeps Npgsql behavior compatible with the pooler.
var csb = new NpgsqlConnectionStringBuilder(conn);
csb.Multiplexing = false;
var connString = csb.ToString();
var pgDataSource = new NpgsqlDataSourceBuilder(connString).Build();
builder.Services.AddSingleton(pgDataSource); // ensure one DataSource for the app
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(pgDataSource, npgsql =>
    {
        npgsql.CommandTimeout(120);
        npgsql.EnableRetryOnFailure(5);
    }));

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
// TheSportsDB v1 (lookuptable, eventsday – key in path). Use same ApiKey as main2 for full eventsday data (free key 123 returns ~5/day).
builder.Services.AddHttpClient("sportsdb-v1", (sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<TheSportsDbOptions>>().Value;
    var v1Key = !string.IsNullOrWhiteSpace(opt.ApiKey) ? opt.ApiKey.Trim() : (opt.ApiKeyV1 ?? "123").Trim();
    client.BaseAddress = new Uri($"{opt.BaseUrlV1.TrimEnd('/')}/{v1Key}/");
    client.Timeout = TimeSpan.FromSeconds(25);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});
builder.Services.AddSingleton<TheSportsDbClient>();
builder.Services.AddSingleton<SportsDbMatchesClient>();
builder.Services.AddScoped<HonoursService>();
builder.Services.AddScoped<HonoursImportService>();
builder.Services.AddScoped<LeagueHonourSeedService>();

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
builder.Services.AddScoped<PlayerRosterService>();

// Video
builder.Services.AddHttpClient("youtube-rss", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});

// Search
builder.Services.AddScoped<FuzzyAliasResolver>();

// If these don’t depend on scoped services/db, singleton is OK
builder.Services.AddSingleton<YouTubeRssClient>();
builder.Services.AddSingleton<VideoAggregator>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapControllers();

if (Environment.GetEnvironmentVariable("BACKFILL_ALIASNORM") == "1")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await AliasNormBackfill.RunAsync(db);
}

app.Run();
