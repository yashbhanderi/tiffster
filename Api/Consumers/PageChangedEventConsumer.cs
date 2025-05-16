using System.ComponentModel.DataAnnotations;
using Api.Domain.Dtos;
using Api.Shared;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Consumers;

public class PageChangedEventConsumer(IMemoryCache memoryCache, ITiffFileHelper tiffFileHelper, IRetryHelper retryHelper) : IConsumer<PageChangedEvent>
{
    public async Task Consume(ConsumeContext<PageChangedEvent> context)
    {
        try
        {
            var pageChangedEvent = context.Message;
            if (pageChangedEvent == null || string.IsNullOrEmpty(pageChangedEvent.SessionName) ||
                pageChangedEvent.PageNumber <= 0)
            {
                throw new ValidationException("Invalid page changed event data.");
            }

            if (memoryCache.TryGetValue(Constants.CurrentPageMemoryCacheKey, out long currentPageNumber) &&
                currentPageNumber == pageChangedEvent.PageNumber)
            {
                // If the page number is the same as the current one, do nothing
                return;
            }

            memoryCache.TryGetValue(Constants.CurrentWindowMemoryCacheKey, out List<long>? currentWindow);

            var tiffFilePath = Path.Combine(Constants.TiffFileStoragePath, $"{pageChangedEvent.SessionName}.tif");

            var newWindow = Utility.GeneratePageWindow((int)pageChangedEvent.PageNumber, Constants.WindowSize, memoryCache.Get<long?>(Constants.TotalPagesCountMemoryCacheKey));
            if (currentWindow is null)
            {
                memoryCache.Set(Constants.CurrentWindowMemoryCacheKey, newWindow);
            }

            var pagesToDelete = currentWindow?.Where(page => page != pageChangedEvent.PageNumber).Except(newWindow).ToList();
            var pagesToAdd = currentWindow?.Count > 0 ? newWindow.Where(page => page != pageChangedEvent.PageNumber && !currentWindow.Contains(page)).ToList() : newWindow.Where(page => page != pageChangedEvent.PageNumber).ToList();

            if (pagesToDelete is { Count: > 0 })
            {
                var retryPolicy = retryHelper.GetRetryPolicy(Constants.RetryCount, Constants.RetryIntervalBaseInSecond, $"{nameof(PageChangedEventConsumer)}: DeleteImagesByPageNumberAsync");   
                await retryPolicy.ExecuteAsync(async () =>
                {
                    // Retry logic for deleting images
                    await tiffFileHelper.DeleteImagesByPageNumberAsync(pagesToDelete);
                });
            }


            if (pagesToAdd is { Count: > 0 })
            {
                var retryPolicy = retryHelper.GetRetryPolicy(Constants.RetryCount, Constants.RetryIntervalBaseInSecond, $"{nameof(PageChangedEventConsumer)}: UploadImagesByPageNumberAsync");   
                await retryPolicy.ExecuteAsync(async () =>
                {
                    // Retry logic for uploading images
                    await tiffFileHelper.UploadImagesByPageNumberAsync(tiffFilePath, pagesToAdd);
                });   
            }

            memoryCache.Set(Constants.CurrentPageMemoryCacheKey, pageChangedEvent.PageNumber);
            memoryCache.Set(Constants.CurrentWindowMemoryCacheKey, newWindow);
        }
        catch (ValidationException e)
        {
            Console.WriteLine(e);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}