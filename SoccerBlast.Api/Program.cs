using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SoccerBlast.Api.Data;
using SoccerBlast.Api.Services;
using SoccerBlast.Api.Services.Video;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.Configure<FootballDataOptions>(
    builder.Configuration.GetSection("FootballData"));

builder.Services.Configure<TheSportsDbOptions>(
    builder.Configuration.GetSection("TheSportsDb"));

builder.Services.Configure<YouTubeSourceOptions>(
    builder.Configuration.GetSection("VideoSources:YouTube"));

//
// Typed HttpClients
//

// football-data.org (timeout 30s – their API can be slow under load)
builder.Services.AddHttpClient<FootballDataClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<FootballDataOptions>>().Value;

    client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);

    if (string.IsNullOrWhiteSpace(opt.ApiToken))
        throw new InvalidOperationException("FootballData:ApiToken is missing. Set it via user-secrets or env var.");

    client.DefaultRequestHeaders.Add("X-Auth-Token", opt.ApiToken);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});

// TheSportsDB – named client "sportsdb" used by TheSportsDbClient via IHttpClientFactory
builder.Services.AddHttpClient("sportsdb", (sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<TheSportsDbOptions>>().Value;
    client.BaseAddress = new Uri($"{opt.BaseUrl.TrimEnd('/')}/{opt.ApiKey.Trim()}/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
});
builder.Services.AddSingleton<TheSportsDbClient>();

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
