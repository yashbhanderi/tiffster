namespace Api.Shared.Dtos;

public class BaseSessionDetails
{
    public bool IsTokenChanged { get; set; }
    public string? NewToken { get; set; }
}