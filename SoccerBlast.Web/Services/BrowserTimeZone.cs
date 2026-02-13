using Microsoft.JSInterop;

namespace SoccerBlast.Web.Services;

public class BrowserTimeZone
{
    private readonly IJSRuntime _js;
    public BrowserTimeZone(IJSRuntime js) => _js = js;

    public ValueTask<string> GetTimeZoneIdAsync()
        => _js.InvokeAsync<string>("tz.getTimeZone");
}