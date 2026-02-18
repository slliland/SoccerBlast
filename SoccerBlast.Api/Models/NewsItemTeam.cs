namespace SoccerBlast.Api.Models;

public class NewsItemTeam
{
    public int NewsItemId { get; set; }
    public NewsItem NewsItem { get; set; } = null!;

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public double Score { get; set; }
}
