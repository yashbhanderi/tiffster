namespace Api.Shared.Dtos;

public class RedisSettings
{
    public string ConnectionString { get; set; }
    public string KeyPrefix { get; set; } = "session:";
}