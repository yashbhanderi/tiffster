using FastEndpoints;

namespace Api.Shared.Authentication;

public class PostProcessors : IGlobalPostProcessor
{
    public Task PostProcessAsync(IPostProcessorContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}