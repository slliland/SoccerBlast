namespace SoccerBlast.Shared.Contracts;

public class VenueDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public string? Country { get; set; }
    public int? Capacity { get; set; }
    public string? ImageUrl { get; set; }
}

