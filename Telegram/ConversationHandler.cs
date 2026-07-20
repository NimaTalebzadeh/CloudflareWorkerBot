using CloudflareWorkerBot.Cloudflare;
using CloudflareWorkerBot.State;
using CloudflareWorkerBot.Templates;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace CloudflareWorkerBot.Telegram;

public sealed class ConversationHandler
{
    private readonly UserSessionManager _sessionManager;
    private readonly CloudflareApiService _cloudflareApi;
    private readonly ILogger<ConversationHandler> _logger;

    public ConversationHandler(
        UserSessionManager sessionManager,
        CloudflareApiService cloudflareApi,
        ILogger<ConversationHandler> logger)
    {
        _sessionManager = sessionManager;
        _cloudflareApi = cloudflareApi;
        _logger = logger;
    }

    public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, CancellationToken ct)
    {
        var session = _sessionManager.GetOrCreateSession(userId);
        session.LastActivity = DateTime.UtcNow;

        if (session.CurrentStep is ConversationStep.Deploying or ConversationStep.Done or ConversationStep.Error)
            return;

        try
        {
            switch (session.CurrentStep)
            {
                case ConversationStep.DeploymentChoice:
                    await HandleDeploymentChoiceStep(bot, chatId, session, text, ct);
                    break;
                case ConversationStep.ApiToken:
                    await HandleApiTokenStep(bot, chatId, session, text, ct);
                    break;
                case ConversationStep.AccountId:
                    await HandleAccountIdStep(bot, chatId, session, text, ct);
                    break;
                case ConversationStep.AdminSecret:
                    await HandleAdminSecretStep(bot, chatId, session, text, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in conversation for user {UserId}", userId);
            session.CurrentStep = ConversationStep.Error;
            await bot.SendMessage(chatId,
                $"<b>An error occurred:</b>\n<code>{EscapeHtml(ex.Message)}</code>\n\nSend /start to try again.",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleDeploymentChoiceStep(ITelegramBotClient bot, long chatId, UserSession session, string input, CancellationToken ct)
    {
        if (input == "1")
        {
            session.DeploymentType = DeploymentType.EdgeTunnel;
            session.CurrentStep = ConversationStep.ApiToken;
            await bot.SendMessage(chatId,
                "<b>Edge Tunnel selected!</b>\n\n" +
                "Open this link to create an API token with the right permissions pre-selected:\n\n" +
                "<a href=\"https://dash.cloudflare.com/profile/api-tokens?permissionGroupKeys=%5B%7B%22key%22%3A%22workers_scripts%22%2C%22type%22%3A%22edit%22%7D%2C%7B%22key%22%3A%22workers_kv_storage%22%2C%22type%22%3A%22edit%22%7D%5D&accountId=*&name=CloudflareWorkerBot-Token\">https://dash.cloudflare.com/profile/api-tokens?...&amp;name=CloudflareWorkerBot-Token</a>\n\n" +
                "Just review the permissions and click <b>Create Token</b>.\n\n" +
                "Send me the token (shown only once!):",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else if (input == "2")
        {
            session.DeploymentType = DeploymentType.Bpb;
            session.CurrentStep = ConversationStep.ApiToken;
            await bot.SendMessage(chatId,
                "<b>BPB Panel selected!</b>\n\n" +
                "Open this link to create an API token with the right permissions pre-selected:\n\n" +
                "<a href=\"https://dash.cloudflare.com/profile/api-tokens?permissionGroupKeys=%5B%7B%22key%22%3A%22workers_scripts%22%2C%22type%22%3A%22edit%22%7D%2C%7B%22key%22%3A%22workers_kv_storage%22%2C%22type%22%3A%22edit%22%7D%5D&accountId=*&name=CloudflareWorkerBot-Token\">https://dash.cloudflare.com/profile/api-tokens?...&amp;name=CloudflareWorkerBot-Token</a>\n\n" +
                "Just review the permissions and click <b>Create Token</b>.\n\n" +
                "Send me the token (shown only once!):",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else if (input == "3")
        {
            session.DeploymentType = DeploymentType.Nahan;
            session.CurrentStep = ConversationStep.ApiToken;
            await bot.SendMessage(chatId,
                "<b>Nahan selected!</b>\n\n" +
                "Open this link to create an API token with the right permissions pre-selected:\n\n" +
                "<a href=\"https://dash.cloudflare.com/profile/api-tokens?permissionGroupKeys=%5B%7B%22key%22%3A%22workers_scripts%22%2C%22type%22%3A%22edit%22%7D%2C%7B%22key%22%3A%22d1%22%2C%22type%22%3A%22edit%22%7D%5D&accountId=*&name=CloudflareWorkerBot-Token\">https://dash.cloudflare.com/profile/api-tokens?...&amp;name=CloudflareWorkerBot-Token</a>\n\n" +
                "Just review the permissions and click <b>Create Token</b>.\n\n" +
                "Send me the token (shown only once!):",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else if (input == "4")
        {
            session.DeploymentType = DeploymentType.Yonggekkk;
            session.CurrentStep = ConversationStep.ApiToken;
            await bot.SendMessage(chatId,
                "<b>Yonggekkk selected!</b>\n\n" +
                "Open this link to create an API token with the right permissions pre-selected:\n\n" +
                "<a href=\"https://dash.cloudflare.com/profile/api-tokens?permissionGroupKeys=%5B%7B%22key%22%3A%22workers_scripts%22%2C%22type%22%3A%22edit%22%7D%5D&accountId=*&name=CloudflareWorkerBot-Token\">https://dash.cloudflare.com/profile/api-tokens?...&amp;name=CloudflareWorkerBot-Token</a>\n\n" +
                "Just review the permissions and click <b>Create Token</b>.\n\n" +
                "Send me the token (shown only once!):",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else if (input == "5")
        {
            session.DeploymentType = DeploymentType.Cfnew;
            session.CurrentStep = ConversationStep.ApiToken;
            await bot.SendMessage(chatId,
                "<b>Cfnew selected!</b>\n\n" +
                "Open this link to create an API token with the right permissions pre-selected:\n\n" +
                                "<a href=\"https://dash.cloudflare.com/profile/api-tokens?permissionGroupKeys=%5B%0A%20%20%7B%20%22key%22%3A%20%22page%22%2C%20%22type%22%3A%20%22edit%22%20%7D%2C%0A%20%20%7B%20%22key%22%3A%20%22workers_kv_storage%22%2C%20%22type%22%3A%20%22edit%22%20%7D%0A%5D&accountId=*&zoneId=all&name=CloudflareWorkerBot-Token\">https://dash.cloudflare.com/profile/api-tokens?...&amp;name=CloudflareWorkerBot-Token</a>\n\n" +
                "Just review the permissions and click <b>Create Token</b>.\n\n" +
                "Send me the token (shown only once!):",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        else
        {
            await bot.SendMessage(chatId, "Please send <b>1</b>, <b>2</b>, <b>3</b>, or <b>4</b>:",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleApiTokenStep(ITelegramBotClient bot, long chatId, UserSession session, string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await bot.SendMessage(chatId, "Please enter a valid API token.", cancellationToken: ct);
            return;
        }
        session.ApiToken = input;

        await bot.SendMessage(chatId, "Fetching your Cloudflare accounts...", cancellationToken: ct);

        try
        {
            var accounts = await _cloudflareApi.GetAccountsAsync(input, ct);

            if (accounts.Count == 0)
            {
                var permHint = session.DeploymentType switch
                {
                    DeploymentType.Nahan => "Workers Scripts Edit and D1 Edit",
                    DeploymentType.Yonggekkk => "Workers Scripts Edit",
                    DeploymentType.Cfnew => "Cloudflare Pages Edit",
                    _ => "Workers Scripts Edit and Workers KV Storage Edit"
                };
                await bot.SendMessage(chatId,
                    $"No accounts found for this token. Make sure the token has <b>{permHint}</b> permissions.\n\nSend /start to try again.",
                    parseMode: ParseMode.Html, cancellationToken: ct);
                session.CurrentStep = ConversationStep.Error;
                return;
            }

            if (accounts.Count == 1)
            {
                session.AccountId = accounts[0].Id;
                session.WorkerName = GenerateWorkerName();

                if (session.DeploymentType == DeploymentType.Bpb)
                {
                    session.AdminSecret = WorkerScript.GenerateTrPass();
                    session.CurrentStep = ConversationStep.Deploying;
                    await bot.SendMessage(chatId,
                        $"<b>Account found:</b> {EscapeHtml(accounts[0].Name)}\n" +
                        $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                        "Starting BPB deployment...",
                        parseMode: ParseMode.Html, cancellationToken: ct);
                    await RunBpbDeploymentAsync(bot, chatId, session, ct);
                }
                else if (session.DeploymentType == DeploymentType.Nahan)
                {
                    session.CurrentStep = ConversationStep.Deploying;
                    await bot.SendMessage(chatId,
                        $"<b>Account found:</b> {EscapeHtml(accounts[0].Name)}\n" +
                        $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                        "Starting Nahan deployment...",
                        parseMode: ParseMode.Html, cancellationToken: ct);
                    await RunNahanDeploymentAsync(bot, chatId, session, ct);
                }
                else if (session.DeploymentType == DeploymentType.Yonggekkk)
                {
                    session.CurrentStep = ConversationStep.Deploying;
                    await bot.SendMessage(chatId,
                        $"<b>Account found:</b> {EscapeHtml(accounts[0].Name)}\n" +
                        $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                        "Starting Yonggekkk deployment...",
                        parseMode: ParseMode.Html, cancellationToken: ct);
                    await RunYonggekkkDeploymentAsync(bot, chatId, session, ct);
                }
                else if (session.DeploymentType == DeploymentType.Cfnew)
                {
                    session.CurrentStep = ConversationStep.Deploying;
                    await bot.SendMessage(chatId,
                        $"<b>Account found:</b> {EscapeHtml(accounts[0].Name)}\n" +
                        $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                        "Starting Cfnew deployment...",
                        parseMode: ParseMode.Html, cancellationToken: ct);
                    await RunCfnewDeploymentAsync(bot, chatId, session, ct);
                }
                else
                {
                    session.CurrentStep = ConversationStep.AdminSecret;
                    await bot.SendMessage(chatId,
                        $"<b>Account found:</b> {EscapeHtml(accounts[0].Name)}\n" +
                        $"<b>Account ID:</b> <code>{accounts[0].Id}</code>\n" +
                        $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                        "Please enter an <b>Admin secret value</b> (stored as the ADMIN variable):",
                        parseMode: ParseMode.Html, cancellationToken: ct);
                }
            }
            else
            {
                var accountList = string.Join("\n", accounts.Select((a, i) =>
                    $"<b>{i + 1}</b> - {EscapeHtml(a.Name)} (<code>{a.Id}</code>)"));

                session.AccountId = null;
                session.CurrentStep = ConversationStep.AccountId;
                await bot.SendMessage(chatId,
                    $"<b>Multiple accounts found:</b>\n\n{accountList}\n\nSend the <b>number</b> of the account to use:",
                    parseMode: ParseMode.Html, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch accounts for user {UserId}", session.UserId);
            await bot.SendMessage(chatId,
                $"<b>Failed to fetch accounts:</b>\n<code>{EscapeHtml(ex.Message)}</code>\n\n" +
                "Please check your token permissions and try again. Send /start to restart.",
                parseMode: ParseMode.Html, cancellationToken: ct);
            session.CurrentStep = ConversationStep.Error;
        }
    }

    private async Task HandleAccountIdStep(ITelegramBotClient bot, long chatId, UserSession session, string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await bot.SendMessage(chatId, "Please enter a valid choice.", cancellationToken: ct);
            return;
        }

        if (int.TryParse(input, out var choice) && choice >= 1)
        {
            try
            {
                var accounts = await _cloudflareApi.GetAccountsAsync(session.ApiToken!, ct);
                if (choice <= accounts.Count)
                {
                    session.AccountId = accounts[choice - 1].Id;
                    session.WorkerName = GenerateWorkerName();

                    if (session.DeploymentType == DeploymentType.Bpb)
                    {
                        session.AdminSecret = WorkerScript.GenerateTrPass();
                        session.CurrentStep = ConversationStep.Deploying;
                        await bot.SendMessage(chatId,
                            $"<b>Account selected:</b> {EscapeHtml(accounts[choice - 1].Name)}\n" +
                            $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                            "Starting BPB deployment...",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                        await RunBpbDeploymentAsync(bot, chatId, session, ct);
                    }
                    else if (session.DeploymentType == DeploymentType.Nahan)
                    {
                        session.CurrentStep = ConversationStep.Deploying;
                        await bot.SendMessage(chatId,
                            $"<b>Account selected:</b> {EscapeHtml(accounts[choice - 1].Name)}\n" +
                            $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                            "Starting Nahan deployment...",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                        await RunNahanDeploymentAsync(bot, chatId, session, ct);
                    }
                    else if (session.DeploymentType == DeploymentType.Yonggekkk)
                    {
                        session.CurrentStep = ConversationStep.Deploying;
                        await bot.SendMessage(chatId,
                            $"<b>Account selected:</b> {EscapeHtml(accounts[choice - 1].Name)}\n" +
                            $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                            "Starting Yonggekkk deployment...",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                        await RunYonggekkkDeploymentAsync(bot, chatId, session, ct);
                    }
                    else if (session.DeploymentType == DeploymentType.Cfnew)
                    {
                        session.CurrentStep = ConversationStep.Deploying;
                        await bot.SendMessage(chatId,
                            $"<b>Account selected:</b> {EscapeHtml(accounts[choice - 1].Name)}\n" +
                            $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                            "Starting Cfnew deployment...",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                        await RunCfnewDeploymentAsync(bot, chatId, session, ct);
                    }
                    else
                    {
                        session.CurrentStep = ConversationStep.AdminSecret;
                        await bot.SendMessage(chatId,
                            $"<b>Account selected:</b> {EscapeHtml(accounts[choice - 1].Name)}\n" +
                            $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                            "Please enter an <b>Admin secret value</b> (stored as the ADMIN variable):",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch accounts for user {UserId}", session.UserId);
            }
        }

        session.AccountId = input;
        session.WorkerName = GenerateWorkerName();

        if (session.DeploymentType == DeploymentType.Bpb)
        {
            session.AdminSecret = WorkerScript.GenerateTrPass();
            session.CurrentStep = ConversationStep.Deploying;
            await bot.SendMessage(chatId,
                $"<b>Account ID saved!</b>\n" +
                $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                "Starting BPB deployment...",
                parseMode: ParseMode.Html, cancellationToken: ct);
            await RunBpbDeploymentAsync(bot, chatId, session, ct);
        }
        else if (session.DeploymentType == DeploymentType.Nahan)
        {
            session.CurrentStep = ConversationStep.Deploying;
            await bot.SendMessage(chatId,
                $"<b>Account ID saved!</b>\n" +
                $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                "Starting Nahan deployment...",
                parseMode: ParseMode.Html, cancellationToken: ct);
            await RunNahanDeploymentAsync(bot, chatId, session, ct);
        }
        else if (session.DeploymentType == DeploymentType.Yonggekkk)
        {
            session.CurrentStep = ConversationStep.Deploying;
            await bot.SendMessage(chatId,
                $"<b>Account ID saved!</b>\n" +
                $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                "Starting Yonggekkk deployment...",
                parseMode: ParseMode.Html, cancellationToken: ct);
            await RunYonggekkkDeploymentAsync(bot, chatId, session, ct);
        }
        else if (session.DeploymentType == DeploymentType.Cfnew)
        {
            session.CurrentStep = ConversationStep.Deploying;
            await bot.SendMessage(chatId,
                $"<b>Account ID saved!</b>\n" +
                $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                "Starting Cfnew deployment...",
                parseMode: ParseMode.Html, cancellationToken: ct);
            await RunCfnewDeploymentAsync(bot, chatId, session, ct);
        }
        else
        {
            session.CurrentStep = ConversationStep.AdminSecret;
            await bot.SendMessage(chatId,
                $"<b>Account ID saved!</b>\n" +
                $"<b>Worker name:</b> <code>{session.WorkerName}</code>\n\n" +
                "Please enter an <b>Admin secret value</b> (stored as the ADMIN variable):",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleAdminSecretStep(ITelegramBotClient bot, long chatId, UserSession session, string input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await bot.SendMessage(chatId, "Please enter a valid value.", cancellationToken: ct);
            return;
        }
        session.AdminSecret = input;
        session.CurrentStep = ConversationStep.Deploying;

        if (session.DeploymentType == DeploymentType.Bpb)
        {
            await bot.SendMessage(chatId,
                "<b>All data collected!</b>\n\nStarting BPB deployment pipeline...",
                parseMode: ParseMode.Html, cancellationToken: ct);
            await RunBpbDeploymentAsync(bot, chatId, session, ct);
        }
        else
        {
            await bot.SendMessage(chatId,
                "<b>All data collected!</b>\n\nStarting Edge Tunnel deployment pipeline...",
                parseMode: ParseMode.Html, cancellationToken: ct);
            await RunEdgeTunnelDeploymentAsync(bot, chatId, session, ct);
        }
    }

    private async Task RunEdgeTunnelDeploymentAsync(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
    {
        var completed = new List<string>();
        var kvName = $"kv-{Guid.NewGuid().ToString("N")[..8]}";

        try
        {
            await bot.SendMessage(chatId,
                "<b>Step 1/4:</b> Deploying Worker script...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var scriptContent = WorkerScript.GetScript(DeploymentType.EdgeTunnel);
            var metadataJson = WorkerScript.GetMetadataJson(DeploymentType.EdgeTunnel);

            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, metadataJson, ct);

            await _cloudflareApi.EnableWorkersDevAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            completed.Add("Worker script deployed");

            await bot.SendMessage(chatId,
                "<b>Step 2/4:</b> Creating KV namespace...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var kvId = await _cloudflareApi.CreateKvNamespaceAsync(
                session.ApiToken!, session.AccountId!, kvName, ct);

            completed.Add($"KV namespace '{kvName}' created");

            await bot.SendMessage(chatId,
                "<b>Step 3/4:</b> Binding KV namespace to Worker...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var boundMetadata = WorkerScript.GetMetadataJson(DeploymentType.EdgeTunnel, kvId);
            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, boundMetadata, ct);

            completed.Add("KV namespace bound to Worker");

            await bot.SendMessage(chatId,
                "<b>Step 4/4:</b> Setting Admin secret...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            await _cloudflareApi.SetSecretViaWranglerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                "ADMIN", session.AdminSecret!, ct);

            completed.Add("Admin secret configured");

            var subdomain = await GetOrClaimSubdomainAsync(session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            var successMsg = string.IsNullOrEmpty(subdomain)
                ? $"""
                    <b>Deployment Complete! (Edge Tunnel)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>

                    <b>URL:</b> Check your Cloudflare dashboard at
                    <a href="https://dash.cloudflare.com">https://dash.cloudflare.com</a>
                    to find your workers.dev subdomain, then visit:
                    <code>https://{session.WorkerName}.&lt;your-subdomain&gt;.workers.dev/admin?key={session.AdminSecret}</code>

                    Send /start to deploy another Worker.
                    """
                : $"""
                    <b>Deployment Complete! (Edge Tunnel)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>

                    <b>Your Worker URL:</b>
                    <a href="https://{session.WorkerName}.{subdomain}.workers.dev/admin?key={session.AdminSecret}">https://{session.WorkerName}.{subdomain}.workers.dev/admin?key={session.AdminSecret}</a>

                    Send /start to deploy another Worker.
                    """;

            await bot.SendMessage(chatId, successMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);

            session.CurrentStep = ConversationStep.Done;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Edge Tunnel deployment failed for user {UserId}", session.UserId);
            session.CurrentStep = ConversationStep.Error;

            var errorMsg = $"""
                <b>Deployment Failed</b>

                Completed steps:
                {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                <b>Error:</b>
                <code>{EscapeHtml(ex.Message)}</code>

                Send /start to try again.
                """;

            await bot.SendMessage(chatId, errorMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task RunBpbDeploymentAsync(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
    {
        var completed = new List<string>();
        var kvName = $"kv-{Guid.NewGuid().ToString("N")[..8]}";
        var uuid = WorkerScript.GenerateUuid();
        var subPath = WorkerScript.GenerateSubPath();

        try
        {
            await bot.SendMessage(chatId,
                "<b>Step 1/5:</b> Deploying Worker script...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var scriptContent = WorkerScript.GetScript(DeploymentType.Bpb);
            var metadataJson = WorkerScript.GetMetadataJson(DeploymentType.Bpb);

            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, metadataJson, ct);

            await _cloudflareApi.EnableWorkersDevAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            completed.Add("Worker script deployed");

            await bot.SendMessage(chatId,
                "<b>Step 2/5:</b> Creating KV namespace...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var kvId = await _cloudflareApi.CreateKvNamespaceAsync(
                session.ApiToken!, session.AccountId!, kvName, ct);

            completed.Add($"KV namespace '{kvName}' created");

            await bot.SendMessage(chatId,
                "<b>Step 3/5:</b> Binding KV namespace to Worker...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var boundMetadata = WorkerScript.GetMetadataJson(DeploymentType.Bpb, kvId);
            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, boundMetadata, ct);

            completed.Add("KV namespace bound to Worker (kv)");

            await bot.SendMessage(chatId,
                "<b>Step 4/5:</b> Setting secrets (UUID, TR_PASS, SUB_PATH)...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            await _cloudflareApi.SetSecretViaWranglerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                "UUID", uuid, ct);

            await _cloudflareApi.SetSecretViaWranglerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                "TR_PASS", session.AdminSecret!, ct);

            await _cloudflareApi.SetSecretViaWranglerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                "SUB_PATH", subPath, ct);

            completed.Add("Secrets configured (UUID, TR_PASS, SUB_PATH)");

            await bot.SendMessage(chatId,
                "<b>Step 5/5:</b> Finalizing...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var subdomain = await GetOrClaimSubdomainAsync(session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            var successMsg = string.IsNullOrEmpty(subdomain)
                ? $"""
                    <b>Deployment Complete! (BPB Panel)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>
                    <b>UUID:</b> <code>{uuid}</code>
                    <b>TR_PASS:</b> <code>{session.AdminSecret}</code>
                    <b>SUB_PATH:</b> <code>{subPath}</code>

                    <b>URLs:</b> Check your Cloudflare dashboard at
                    <a href="https://dash.cloudflare.com">https://dash.cloudflare.com</a>
                    to find your workers.dev subdomain, then visit:
                    <code>https://{session.WorkerName}.&lt;your-subdomain&gt;.workers.dev/panel</code>

                    Send /start to deploy another Worker.
                    """
                : $"""
                    <b>Deployment Complete! (BPB Panel)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>
                    <b>UUID:</b> <code>{uuid}</code>
                    <b>TR_PASS:</b> <code>{session.AdminSecret}</code>
                    <b>SUB_PATH:</b> <code>{subPath}</code>

                    <b>Panel URL:</b>
                    <a href="https://{session.WorkerName}.{subdomain}.workers.dev/panel">https://{session.WorkerName}.{subdomain}.workers.dev/panel</a>

                    <b>Subscription URL:</b>
                    <a href="https://{session.WorkerName}.{subdomain}.workers.dev/sub/normal/{subPath}?app=xray#%F0%9F%92%A6%20BPB%20Normal">https://{session.WorkerName}.{subdomain}.workers.dev/sub/normal/{subPath}?app=xray#BPB Normal</a>

                    Send /start to deploy another Worker.
                    """;

            await bot.SendMessage(chatId, successMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);

            session.CurrentStep = ConversationStep.Done;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BPB deployment failed for user {UserId}", session.UserId);
            session.CurrentStep = ConversationStep.Error;

            var errorMsg = $"""
                <b>Deployment Failed</b>

                Completed steps:
                {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                <b>Error:</b>
                <code>{EscapeHtml(ex.Message)}</code>

                Send /start to try again.
                """;

            await bot.SendMessage(chatId, errorMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task RunNahanDeploymentAsync(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
    {
        var completed = new List<string>();
        var d1Name = $"d1-{Guid.NewGuid().ToString("N")[..8]}";

        try
        {
            await bot.SendMessage(chatId,
                "<b>Step 1/4:</b> Deploying Worker script...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var scriptContent = WorkerScript.GetScript(DeploymentType.Nahan);
            var metadataJson = WorkerScript.GetMetadataJson(DeploymentType.Nahan);

            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, metadataJson, ct);

            await _cloudflareApi.EnableWorkersDevAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            completed.Add("Worker script deployed");

            await bot.SendMessage(chatId,
                "<b>Step 2/4:</b> Creating D1 database...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var d1Uuid = await _cloudflareApi.CreateD1DatabaseAsync(
                session.ApiToken!, session.AccountId!, d1Name, ct);

            completed.Add($"D1 database '{d1Name}' created");

            await bot.SendMessage(chatId,
                "<b>Step 3/4:</b> Binding D1 database to Worker...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var boundMetadata = WorkerScript.GetMetadataJson(DeploymentType.Nahan, d1Uuid);
            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, boundMetadata, ct);

            completed.Add("D1 database bound to Worker (IOT_DB)");

            await bot.SendMessage(chatId,
                "<b>Step 4/4:</b> Finalizing...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var subdomain = await GetOrClaimSubdomainAsync(session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            var successMsg = string.IsNullOrEmpty(subdomain)
                ? $"""
                    <b>Deployment Complete! (Nahan)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>
                    <b>Admin password:</b> <code>admin</code>

                    <b>URLs:</b> Check your Cloudflare dashboard at
                    <a href="https://dash.cloudflare.com">https://dash.cloudflare.com</a>
                    to find your workers.dev subdomain, then visit:
                    <code>https://{session.WorkerName}.&lt;your-subdomain&gt;.workers.dev/sync/dash</code>

                    Send /start to deploy another Worker.
                    """
                : $"""
                    <b>Deployment Complete! (Nahan)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>
                    <b>Admin password:</b> <code>admin</code>

                    <b>Panel URL:</b>
                    <a href="https://{session.WorkerName}.{subdomain}.workers.dev/sync/dash">https://{session.WorkerName}.{subdomain}.workers.dev/sync/dash</a>

                    <b>Subscription URL:</b>
                    <a href="https://{session.WorkerName}.{subdomain}.workers.dev/sync">https://{session.WorkerName}.{subdomain}.workers.dev/sync</a>

                    Send /start to deploy another Worker.
                    """;

            await bot.SendMessage(chatId, successMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);

            session.CurrentStep = ConversationStep.Done;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nahan deployment failed for user {UserId}", session.UserId);
            session.CurrentStep = ConversationStep.Error;

            var errorMsg = $"""
                <b>Deployment Failed</b>

                Completed steps:
                {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                <b>Error:</b>
                <code>{EscapeHtml(ex.Message)}</code>

                Send /start to try again.
                """;

            await bot.SendMessage(chatId, errorMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task RunYonggekkkDeploymentAsync(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
    {
        var completed = new List<string>();

        try
        {
            await bot.SendMessage(chatId,
                "<b>Step 1/3:</b> Deploying Worker script...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var scriptContent = WorkerScript.GetScript(DeploymentType.Yonggekkk);
            var metadataJson = WorkerScript.GetMetadataJson(DeploymentType.Yonggekkk);

            await _cloudflareApi.DeployWorkerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                scriptContent, metadataJson, ct);

            await _cloudflareApi.EnableWorkersDevAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            completed.Add("Worker script deployed");

            await bot.SendMessage(chatId,
                "<b>Step 2/3:</b> Setting UUID secret...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var uuid = WorkerScript.GenerateUuid();
            await _cloudflareApi.SetSecretViaWranglerAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!,
                "uuid", uuid, ct);

            completed.Add("UUID secret configured");

            await bot.SendMessage(chatId,
                "<b>Step 3/3:</b> Finalizing...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var subdomain = await GetOrClaimSubdomainAsync(session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            var successMsg = string.IsNullOrEmpty(subdomain)
                ? $"""
                    <b>Deployment Complete! (Yonggekkk)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>
                    <b>UUID:</b> <code>{uuid}</code>

                    <b>URLs:</b> Check your Cloudflare dashboard at
                    <a href="https://dash.cloudflare.com">https://dash.cloudflare.com</a>
                    to find your workers.dev subdomain.

                    Send /start to deploy another Worker.
                    """
                : $"""
                    <b>Deployment Complete! (Yonggekkk)</b>

                    {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                    <b>Worker name:</b> <code>{session.WorkerName}</code>
                    <b>UUID:</b> <code>{uuid}</code>

                    <b>Worker URL:</b>
                    <a href="https://{session.WorkerName}.{subdomain}.workers.dev">https://{session.WorkerName}.{subdomain}.workers.dev</a>

                    Send /start to deploy another Worker.
                    """;

            await bot.SendMessage(chatId, successMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);

            session.CurrentStep = ConversationStep.Done;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yonggekkk deployment failed for user {UserId}", session.UserId);
            session.CurrentStep = ConversationStep.Error;

            var errorMsg = $"""
                <b>Deployment Failed</b>

                Completed steps:
                {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                <b>Error:</b>
                <code>{EscapeHtml(ex.Message)}</code>

                Send /start to try again.
                """;

            await bot.SendMessage(chatId, errorMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task RunCfnewDeploymentAsync(ITelegramBotClient bot, long chatId, UserSession session, CancellationToken ct)
    {
        var completed = new List<string>();
        var uuid = WorkerScript.GenerateUuid();

        try
        {
            await bot.SendMessage(chatId,
                "<b>Step 1/5:</b> Creating Pages project...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            // 1. Create Pages project (or use existing)
            await _cloudflareApi.CreatePagesProjectAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, ct);

            completed.Add("Pages project created");

            await bot.SendMessage(chatId,
                "<b>Step 2/5:</b> Creating KV namespace & binding...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            // 2. Create KV namespace + bind to Pages project + set UUID var 'u' in one patch
            await _cloudflareApi.ConfigurePagesProjectAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, uuid, ct);

            completed.Add("KV namespace bound and UUID variable 'u' configured");

            await bot.SendMessage(chatId,
                "<b>Step 4/5:</b> Uploading Pages deployment...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            // 4. Upload _worker.js to Pages deployment
            var scriptContent = WorkerScript.GetScript(DeploymentType.Cfnew);

            await _cloudflareApi.UploadPagesDeploymentAsync(
                session.ApiToken!, session.AccountId!, session.WorkerName!, scriptContent, ct);

            completed.Add("Pages deployment uploaded");

            await bot.SendMessage(chatId,
                "<b>Step 5/5:</b> Finalizing...",
                parseMode: ParseMode.Html, cancellationToken: ct);

            var successMsg = $"""
                <b>Deployment Complete! (Cfnew - Pages)</b>

                {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                <b>Project name:</b> <code>{session.WorkerName}</code>

                <b>Dashboard URL:</b>
                <a href="https://{session.WorkerName}.pages.dev/{uuid}">https://{session.WorkerName}.pages.dev/{uuid}</a>

                <b>UUID:</b> <code>{uuid}</code>

                Save your UUID — you'll need it to access the panel.

                Send /start to deploy another Worker.
                """;

            await bot.SendMessage(chatId, successMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);

            session.CurrentStep = ConversationStep.Done;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cfnew Pages deployment failed for user {UserId}", session.UserId);
            session.CurrentStep = ConversationStep.Error;

            var errorMsg = $"""
                <b>Deployment Failed</b>

                Completed steps:
                {string.Join("\n", completed.Select(s => $"  \u2713 {s}"))}

                <b>Error:</b>
                <code>{EscapeHtml(ex.Message)}</code>

                Send /start to try again.
                """;

            await bot.SendMessage(chatId, errorMsg,
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private static byte[] CreatePagesZip(string scriptContent)
    {
        using var ms = new System.IO.MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            // Add _worker.js
            var workerEntry = archive.CreateEntry("_worker.js");
            using (var writer = new System.IO.StreamWriter(workerEntry.Open()))
            {
                writer.Write(scriptContent);
            }

            // Add wrangler.toml
            var tomlEntry = archive.CreateEntry("wrangler.toml");
            using (var writer = new System.IO.StreamWriter(tomlEntry.Open()))
            {
                writer.Write("compatibility_date = \"2026-01-20\"\n");
            }
        }
        return ms.ToArray();
    }

    private static string GenerateWorkerName()
    {
        var random = Random.Shared.Next(0, 0xFFFFFF);
        return $"worker-{random:x6}";
    }

    private async Task<string> GetOrClaimSubdomainAsync(string apiToken, string accountId, string workerName, CancellationToken ct)
    {
        var subdomain = await _cloudflareApi.GetWorkersDevSubdomainAsync(apiToken, accountId, ct);
        if (!string.IsNullOrEmpty(subdomain))
            return subdomain;

        // Try to claim a subdomain based on the worker name
        var candidate = workerName.Replace("worker-", "").Replace("-", "");
        if (candidate.Length < 3)
            candidate = $"cfworker{Random.Shared.Next(100, 999)}";

        var claimed = await _cloudflareApi.ClaimSubdomainAsync(apiToken, accountId, candidate, ct);
        if (!string.IsNullOrEmpty(claimed))
            return claimed;

        // Try once more with a random suffix
        var fallback = $"cfw{Random.Shared.Next(10000, 99999)}";
        claimed = await _cloudflareApi.ClaimSubdomainAsync(apiToken, accountId, fallback, ct);
        return claimed;
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
