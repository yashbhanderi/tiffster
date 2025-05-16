namespace Api.Domain.V1.Authentication.StartSession;

public class StartSessionResponseDto
{
    public string SessionName { get; set; }
    public string Token { get; set; }
    public long ExpiryTime { get; set; }
}