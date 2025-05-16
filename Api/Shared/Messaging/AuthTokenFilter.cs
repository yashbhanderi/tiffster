using MassTransit;
using Microsoft.Extensions.Options;

namespace Api.Shared.Messaging;

public class AuthTokenFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly RabbitMqConfig _config;

    public AuthTokenFilter(IOptions<RabbitMqConfig> config)
    {
        _config = config.Value;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var token = context.Headers.Get<string>("Authorization");

        if (string.IsNullOrEmpty(token) || !IsValidToken(token))
        {
            // You can log, throw, or just swallow based on your preference
            throw new UnauthorizedAccessException("Invalid or missing Authorization token.");
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context) => context.CreateFilterScope("AuthTokenFilter");

    private bool IsValidToken(string token)
    {
        // Your custom token validation logic here
        return token == $"Bearer {_config.AuthToken}"; // Simplified
    }
}