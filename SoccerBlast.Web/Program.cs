using SoccerBlast.Shared.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// BaseAddress points to your API URL (adjust port to match your API)
builder.Services.AddHttpClient("SoccerApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:5001/");
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
