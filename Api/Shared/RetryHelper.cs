using FluentValidation;
using Polly;

namespace Api.Shared;

public class RetryHelper(ILogger<RetryHelper> logger) : IRetryHelper
{
    /// <summary>
    /// 
    /// This will retry for 5(retryCount) times with 2(retryIntervalBaseForPow)^retryAttempts delay
    /// Retry Attempts | Delay in seconds | Total seconds elapsed
    ///     1          |     2            |        2
    ///     2          |     4            |        6
    ///     3          |     8            |        14
    ///     4          |     16           |        30
    ///     5          |     32           |        62
    /// 
    /// </summary>
    /// <param name="retryCount">Count of retries to be made</param>
    /// <param name="retryIntervalBaseForPow">Retry intervals to be calculated based on this property raised to the number of retry being done.</param>
    /// <param name="retryName"></param>
    /// <returns></returns>
    public Polly.Retry.AsyncRetryPolicy GetRetryPolicy(int retryCount, int retryIntervalBaseForPow, string retryName)
    {
        return Policy.Handle<Exception>(exception => exception is not ValidationException)
            .WaitAndRetryAsync(retryCount,
                retryAttempts => TimeSpan.FromSeconds(Math.Pow(retryIntervalBaseForPow, retryAttempts)),
                (exception, timeSpan, count, context) =>
                {
                    logger.LogInformation(
                        "Retry via Polly with timespan {@timespan}, Exception details: {@exceptionDetails} and Retry Count: {retryCount}, Retry Name: {retryName}",
                        timeSpan, exception.Message, count, retryName);
                });
    }
}

public interface IRetryHelper
{
    public Polly.Retry.AsyncRetryPolicy GetRetryPolicy(int retryCount, int retryIntervalBaseForPow, string retryName);
}