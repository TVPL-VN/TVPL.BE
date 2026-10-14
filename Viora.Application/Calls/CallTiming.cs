namespace Viora.Application.Calls;

public static class CallTiming
{
    public static readonly TimeSpan RingingTimeout = TimeSpan.FromSeconds(30);

    public static bool IsExpired(DateTime startedAt, DateTime now) =>
        now - startedAt >= RingingTimeout;
}
