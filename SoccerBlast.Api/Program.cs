using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using Microsoft.Extensions.Options;
using SoccerBlast.Api.Services;
using SoccerBlast.Api.Services.Video;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add EF Core with SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register HttpClient for football-data.org
builder.Services.Configure<FootballDataOptions>(
    builder.Configuration.GetSection("FootballData"));

builder.Services.AddHttpClient<FootballDataClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<FootballDataOptions>>().Value;

    client.BaseAddress = new Uri(opt.BaseUrl);

    if (string.IsNullOrWhiteSpace(opt.ApiToken))
        throw new InvalidOperationException("FootballData:ApiToken is missing. Set it via user-secrets or env var.");

    client.DefaultRequestHeaders.Add("X-Auth-Token", opt.ApiToken);
});


builder.Services.AddScoped<MatchSyncService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<NewsService>();
builder.Services.AddScoped<NewsService>();

// Video
// - cache
builder.Services.AddMemoryCache();
// outbound HTTP
builder.Services.AddHttpClient("youtube-rss", c =>
{
    c.Timeout = TimeSpan.FromSeconds(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SoccerBlast/1.0");
}); // IHttpClientFactory best practice :contentReference[oaicite:6]{index=6}

// options binding
builder.Services.Configure<YouTubeSourceOptions>(
    builder.Configuration.GetSection("VideoSources:YouTube"));

// services
builder.Services.AddSingleton<YouTubeRssClient>();
builder.Services.AddSingleton<VideoAggregator>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();