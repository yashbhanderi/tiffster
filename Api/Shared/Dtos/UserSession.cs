namespace Api.Domain.Dtos;

public class UserSession
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public Guid SessionName { get; set; }

    /// <summary>
    /// Unix timestamp in seconds representing when the session expires
    /// Using Unix timestamps for easy comparison across C# and JavaScript
    /// </summary>
    public long ExpiryTime { get; set; }

    public UserSession()
    {
    }

    public UserSession(Guid sessionName, long expiryTimeInSeconds)
    {
        SessionName = sessionName;
        ExpiryTime = expiryTimeInSeconds;
    }

    /// <summary>
    /// Creates a new session with the specified expiry duration in minutes
    /// </summary>
    public static UserSession Create(int expiryMinutes = 60)
    {
        return new UserSession
        {
            SessionName = Guid.NewGuid(),
            // Convert to Unix timestamp (seconds since epoch)
            ExpiryTime = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds()
        };
    }

    /// <summary>
    /// Check if the session has expired
    /// </summary>
    public bool HasExpired()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= ExpiryTime;
    }

    /// <summary>
    /// Extend the session by specified minutes
    /// </summary>
    public UserSession ExtendSession(int additionalMinutes = 60)
    {
        return new UserSession
        {
            SessionName = this.SessionName,
            ExpiryTime = DateTimeOffset.UtcNow.AddMinutes(additionalMinutes).ToUnixTimeSeconds()
        };
    }
}