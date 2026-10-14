namespace Viora.Application.Realtime;

public static class PushNotificationPolicy
{
    public static bool IsLifecycle(IReadOnlyDictionary<string, string> data) =>
        data.TryGetValue("type", out var type) && type is "CallRejected" or "CallCancelled" or "CallEnded" or "CallAnsweredElsewhere"
            or "CallMissed" or "CallTimeout" or "GroupCallEnded";

    public static bool ShouldDeliver(int disabledKinds, IReadOnlyDictionary<string, string> data)
    {
        data.TryGetValue("type", out var type);
        if (IsLifecycle(data)) return true;
        var kind = type switch
        {
            "chat" => data.TryGetValue("conversationType", out var conversationType) && conversationType == "1" ? 2 : 1,
            "IncomingCall" or "GroupCall" or "MissedCall" => data.TryGetValue("callType", out var callType) && callType == "1" ? 64 : 32,
            _ => NotificationKind(data)
        };
        return (disabledKinds & kind) == 0;
    }

    private static int NotificationKind(IReadOnlyDictionary<string, string> data)
    {
        data.TryGetValue("notificationType", out var value);
        return value switch
        {
            "1" or "2" or "20" => 16,
            "4" or "5" or "6" or "8" or "19" or "21" => 8,
            _ => 4
        };
    }
}
