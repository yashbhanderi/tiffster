namespace Api.Shared.Dtos;

public class SessionSettings
{
    public int SessionDurationMinutes { get; set; } = 43200;
    public int RenewalDurationMinutes { get; set; } = 43200;
    public string[] ExcludedPaths { get; set; } = ["/v1/api/session/start"];
}