namespace PokemonMMO.Models.DTOs;

public class RankLeaderboardEntryDto
{
    public int Rank { get; set; }
    public string PlayerId { get; set; } = null!;
    public string PlayerName { get; set; } = null!;
    public int Score { get; set; }
    public int RankPoints { get; set; }
    public int RankedWins { get; set; }
    public int RankedMatches { get; set; }
    public bool IsSelf { get; set; }
}
