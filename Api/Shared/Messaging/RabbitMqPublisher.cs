using MassTransit;
using Microsoft.Extensions.Options;

namespace Api.Shared.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : BaseEvent;
}

// 5. MassTransit event publisher implementation
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly RabbitMqConfig _config;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint, IOptions<RabbitMqConfig> config)
    {
        _publishEndpoint = publishEndpoint;
        _config = config.Value;
    }

    public Task PublishAsync<TEvent>(TEvent @event,
        CancellationToken cancellationToken = default) where TEvent : BaseEvent
    {
        return _publishEndpoint.Publish(@event,
            context => { context.Headers.Set("Authorization", $"Bearer {_config.AuthToken}"); }, cancellationToken);
    }
}