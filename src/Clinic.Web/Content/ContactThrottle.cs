using Microsoft.Extensions.Caching.Memory;

namespace Clinic.Web.Content;

/// <summary>Limits contact-form submissions per IP address to stop floods of spam.</summary>
public class ContactThrottle(IMemoryCache cache, TimeProvider time)
{
    public const int MaxPerWindow = 3;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Lock _lock = new();

    /// <returns>False when this address has already sent the maximum in the current window.</returns>
    public bool TryAcquire(string? ipAddress)
    {
        var key = "contact:" + (ipAddress ?? "unknown");
        lock (_lock)
        {
            var now = time.GetUtcNow();
            var sent = cache.Get<List<DateTimeOffset>>(key) ?? [];
            sent.RemoveAll(t => now - t >= Window);
            if (sent.Count >= MaxPerWindow)
            {
                return false;
            }
            sent.Add(now);
            cache.Set(key, sent, Window);
            return true;
        }
    }
}
