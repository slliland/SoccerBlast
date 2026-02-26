using Microsoft.AspNetCore.SignalR;
using SoccerBlast.Web.Services;
using SoccerBlast.Web.Components;
using SoccerBlast.Web.Services.Video;

var builder = WebApplication.CreateBuilder(args);

// Allow large payloads (e.g. hundreds of matches) over SignalR; default 32KB truncates the list.
builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 2 * 1024 * 1024; // 2 MB
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API client configured from appsettings
builder.Services.AddHttpClient<SoccerApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5249/";
    client.BaseAddress = new Uri(baseUrl);
    // Match sync can take 60–120s (SportsDB API + DB + pooler); allow enough time to avoid client cancel
    client.Timeout = TimeSpan.FromSeconds(180);
});

builder.Services.AddScoped<BrowserTimeZone>();
builder.Services.AddScoped<UserPrefsStore>();
builder.Services.AddScoped<DashboardDataService>();
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5249/";

builder.Services.AddHttpClient<IVideoSource, ApiVideoSource>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
