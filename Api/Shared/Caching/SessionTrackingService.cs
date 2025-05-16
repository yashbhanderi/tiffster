using Api.Domain.Dtos;
using Api.Shared.Authentication;
using Api.Shared.Dtos;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Api.Shared.Caching;

public class SessionTrackingService
{
    private readonly ConnectionMultiplexer _redis;
    private readonly JwtService _jwtService;
    private readonly string _keyPrefix;

    public SessionTrackingService(
        ConnectionMultiplexer redis,
        JwtService jwtService,
        IOptions<RedisSettings> redisSettings)
    {
        _redis = redis;
        _jwtService = jwtService;
        _keyPrefix = redisSettings.Value.KeyPrefix ?? "session:";
    }

    private string GetSessionKey(Guid sessionName)
    {
        return $"{_keyPrefix}{sessionName}";
    }

    /// <summary>
    /// Store session in Redis with its expiry time
    /// </summary>
    public async Task StoreSessionAsync(UserSession session)
    {
        var db = _redis.GetDatabase();
        string key = GetSessionKey(session.SessionName);

        // Store basic session info in Redis
        // Store expiry directly as a number for simple retrieval
        await db.StringSetAsync(key, session.ExpiryTime.ToString(),
            expiry: DateTimeOffset.FromUnixTimeSeconds(session.ExpiryTime + 300).DateTime - DateTime.UtcNow);
    }

    /// <summary>
    /// Check if a session exists in Redis
    /// </summary>
    public async Task<bool> SessionExistsAsync(Guid sessionName)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync(GetSessionKey(sessionName));
    }

    /// <summary>
    /// Get session expiry time from Redis
    /// </summary>
    public async Task<long?> GetSessionExpiryAsync(Guid sessionName)
    {
        var db = _redis.GetDatabase();
        string expiryStr = await db.StringGetAsync(GetSessionKey(sessionName));

        if (string.IsNullOrEmpty(expiryStr))
            return null;

        if (long.TryParse(expiryStr, out long expiryTime))
            return expiryTime;

        return null;
    }

    /// <summary>
    /// Remove a session from Redis
    /// </summary>
    public async Task RemoveSessionAsync(Guid sessionName)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(GetSessionKey(sessionName));
    }

    /// <summary>
    /// Get all session keys that need to be checked for expiry
    /// </summary>
    public async Task<List<Guid>> GetAllSessionsAsync()
    {
        var db = _redis.GetDatabase();
        var keys = new List<Guid>();

        // Get all keys with our prefix
        foreach (var key in _redis.GetServer(_redis.GetEndPoints()[0]).Keys(pattern: $"{_keyPrefix}*"))
        {
            string keyStr = key.ToString();
            string guidStr = keyStr.Substring(_keyPrefix.Length);

            if (Guid.TryParse(guidStr, out Guid sessionGuid))
            {
                keys.Add(sessionGuid);
            }
        }

        return keys;
    }
}