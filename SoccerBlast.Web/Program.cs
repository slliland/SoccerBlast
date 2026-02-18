using SoccerBlast.Web.Services;
using SoccerBlast.Web.Components;
using SoccerBlast.Web.Services.Video;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API client configured from appsettings
builder.Services.AddHttpClient<SoccerApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5249/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(12);
});

builder.Services.AddScoped<BrowserTimeZone>();
builder.Services.AddScoped<UserPrefsStore>();
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
