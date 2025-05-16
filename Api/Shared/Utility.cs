namespace Api.Shared;

public static class Utility
{
    public static bool IsEmpty(this string str)
    {
        return string.IsNullOrEmpty(str) || string.IsNullOrWhiteSpace(str);
    }

    public static void CheckIfTokenChanged(this HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue("NewToken", out var newTokenObj) && newTokenObj is string newToken)
        {
            httpContext.Response.Headers.Add("X-Token-Changed", "true");
            httpContext.Response.Headers.Add("X-New-Token", newToken);
        }
    }

    public static List<long> GeneratePageWindow(long currentPage, int window, long? endLimit = null)
    {
        var halfWindow = window / 2;
        var start = Math.Max(1, currentPage - halfWindow);
        var end = start + window - 1;

        if (endLimit.HasValue && end > endLimit.Value)
        {
            end = endLimit.Value;
            start = Math.Max(1, end - window + 1);
        }

        return Enumerable.Range(0, Math.Min(window, (int)(end - start + 1))).Select(i => start + i).ToList();
    }
}