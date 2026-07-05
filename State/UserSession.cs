namespace CloudflareWorkerBot.State;

public sealed class UserSession
{
    public long UserId { get; init; }
    public ConversationStep CurrentStep { get; set; } = ConversationStep.Start;
    public DeploymentType DeploymentType { get; set; }
    public string? ApiToken { get; set; }
    public string? AccountId { get; set; }
    public string? WorkerName { get; set; }
    public string? AdminSecret { get; set; }
    public string? KvName { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public void Reset()
    {
        CurrentStep = ConversationStep.Start;
        DeploymentType = DeploymentType.EdgeTunnel;
        ApiToken = null;
        AccountId = null;
        WorkerName = null;
        AdminSecret = null;
        KvName = null;
        LastActivity = DateTime.UtcNow;
    }
}
