using MassTransit;

namespace Api.Shared.Messaging;

public interface IEventConsumer<in TEvent> where TEvent : BaseEvent
{
    Task Consume(TEvent @event, CancellationToken cancellationToken);
}

// 3. MassTransit consumer adapter - connects our consumer pattern to MassTransit
public class MassTransitConsumerAdapter<TEvent> : IConsumer<TEvent> where TEvent : BaseEvent
{
    private readonly IEventConsumer<TEvent> _consumer;

    public MassTransitConsumerAdapter(IEventConsumer<TEvent> consumer)
    {
        _consumer = consumer;
    }

    public Task Consume(ConsumeContext<TEvent> context)
    {
        return _consumer.Consume(context.Message, context.CancellationToken);
    }
}