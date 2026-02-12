using Microsoft.EntityFrameworkCore;
using SoccerBlast.Api.Data;
using Microsoft.Extensions.Options;
using SoccerBlast.Api.Services;

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

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();