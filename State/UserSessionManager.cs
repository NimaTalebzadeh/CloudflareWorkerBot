using System.Collections.Concurrent;

namespace CloudflareWorkerBot.State;

public sealed class UserSessionManager
{
    private readonly ConcurrentDictionary<long, UserSession> _sessions = new();
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    public UserSession GetOrCreateSession(long userId)
    {
        return _sessions.GetOrAdd(userId, id => new UserSession { UserId = id });
    }

    public bool TryGetSession(long userId, out UserSession session)
    {
        if (_sessions.TryGetValue(userId, out session!))
        {
            if (DateTime.UtcNow - session.LastActivity > SessionTimeout)
                session.Reset();
            return true;
        }
        session = null!;
        return false;
    }

    public void ResetSession(long userId)
    {
        if (_sessions.TryGetValue(userId, out var session))
            session.Reset();
    }
}
