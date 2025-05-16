using Api.Shared.Messaging;

namespace Api.Domain.Dtos;

public class PageChangedEvent : BaseEvent
{
    public long PageNumber { get; set; }
    public long? Index { get; set; }
    public string SessionName { get; set; }
}