namespace CloudflareWorkerBot.State;

public enum ConversationStep
{
    Start,
    DeploymentChoice,
    ApiToken,
    AccountId,
    AdminSecret,
    Deploying,
    Done,
    Error,
    AwaitingIpUploadReplace,
    AwaitingIpUploadAppend,
    AwaitingConfigTemplates
}
