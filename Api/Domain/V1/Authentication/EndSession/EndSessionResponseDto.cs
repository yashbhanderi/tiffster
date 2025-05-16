namespace Api.Domain.V1.Authentication.EndSession;

public class EndSessionResponseDto
{
    public string Token { get; set; }
    public long ExpiryTime { get; set; }
}