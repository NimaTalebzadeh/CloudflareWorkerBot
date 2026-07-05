using System.Text;
using System.Text.RegularExpressions;
using CloudflareWorkerBot.Cloudflare;
using CloudflareWorkerBot.Services;
using CloudflareWorkerBot.State;
using CloudflareWorkerBot.Templates;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CloudflareWorkerBot.Telegram;

public sealed class BotUpdateRouter
{
    private readonly UserSessionManager _sessionManager;
    private readonly ConversationHandler _conversationHandler;
    private readonly CloudflareApiService _cloudflareApi;
    private readonly CleanIpCache _cleanIpCache;
    private readonly ILogger<BotUpdateRouter> _logger;

    public BotUpdateRouter(
        UserSessionManager sessionManager,
        ConversationHandler conversationHandler,
        CloudflareApiService cloudflareApi,
        CleanIpCache cleanIpCache,
        ILogger<BotUpdateRouter> logger)
    {
        _sessionManager = sessionManager;
        _conversationHandler = conversationHandler;
        _cloudflareApi = cloudflareApi;
        _cleanIpCache = cleanIpCache;
        _logger = logger;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            if (update.CallbackQuery is { } callback)
            {
                await HandleCallbackQueryAsync(bot, callback, ct);
                return;
            }

            var message = update.Message;
            if (message is null) return;

            var chatId = message.Chat.Id;
            var userId = message.From?.Id ?? 0;

            if (message.Document is not null)
            {
                await HandleDocumentAsync(bot, message, userId, ct);
                return;
            }

            if (message.Text is null) return;

            var text = message.Text.Trim();

            if (text.StartsWith("/"))
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLowerInvariant();

                switch (command)
                {
                    case "/start":
                        _sessionManager.ResetSession(userId);
                        var session = _sessionManager.GetOrCreateSession(userId);
                        session.LastActivity = DateTime.UtcNow;
                        var startKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Deploy a Worker", "cmd:deployoptions") },
                            new[] { InlineKeyboardButton.WithCallbackData("Get Clean IPs", "cmd:cleanip"), InlineKeyboardButton.WithCallbackData("Generate Configs", "cmd:config") },
                            new[] { InlineKeyboardButton.WithCallbackData("Help", "cmd:help") }
                        });
                        await bot.SendMessage(chatId,
                            "<b>CloudflareWorkerBot</b>\n\n" +
                            "This bot deploys Cloudflare Workers to your account. " +
                            "You can deploy proxy tunnels, VPN panels, and more.\n\n" +
                            "<b>You need a free Cloudflare account.</b>\n" +
                            "Sign up: <a href=\"https://dash.cloudflare.com/sign-up\">cloudflare.com/sign-up</a>",
                            parseMode: ParseMode.Html, replyMarkup: startKeyboard, cancellationToken: ct);
                        return;

                    case "/deployoptions":
                        _sessionManager.ResetSession(userId);
                        var deploySession = _sessionManager.GetOrCreateSession(userId);
                        deploySession.CurrentStep = ConversationStep.DeploymentChoice;
                        deploySession.LastActivity = DateTime.UtcNow;
                        var deployKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("1", "deploy:1"), InlineKeyboardButton.WithCallbackData("2", "deploy:2") },
                            new[] { InlineKeyboardButton.WithCallbackData("3", "deploy:3"), InlineKeyboardButton.WithCallbackData("4", "deploy:4") }
                        });
                        await bot.SendMessage(chatId,
                            "<b>What would you like to deploy?</b>\n\n" +
                            "1 - Edge Tunnel\n" +
                            "2 - BPB Panel\n" +
                            "3 - Nahan\n" +
                            "4 - Yonggekkk",
                            replyMarkup: deployKeyboard, cancellationToken: ct);
                        return;

                    case "/help":
                        await HandleHelpAsync(bot, chatId, ct);
                        return;

                    case "/cancel":
                    case "/reset":
                        _sessionManager.ResetSession(userId);
                        await bot.SendMessage(chatId,
                            "<b>Session cancelled.</b>\n\nSend /start to begin again.",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                        return;

                    case "/status":
                        if (parts.Length > 1)
                            await HandleWorkerStatusAsync(bot, chatId, userId, parts[1], ct);
                        else
                            await HandleSessionStatusAsync(bot, chatId, userId, ct);
                        return;

                    case "/list":
                        await HandleListWorkersAsync(bot, chatId, userId, ct);
                        return;

                    case "/delete":
                        if (parts.Length < 2)
                        {
                            await bot.SendMessage(chatId, "Usage: /delete <worker-name>",
                                cancellationToken: ct);
                            return;
                        }
                        await HandleDeleteWorkerAsync(bot, chatId, userId, parts[1], ct);
                        return;

                    case "/analytics":
                        if (parts.Length < 2)
                        {
                            await bot.SendMessage(chatId, "Usage: /analytics <worker-name>",
                                cancellationToken: ct);
                            return;
                        }
                        await HandleAnalyticsAsync(bot, chatId, userId, parts[1], ct);
                        return;

                    case "/destroy":
                        await HandleDestroyAsync(bot, chatId, userId, ct);
                        return;

                    case "/info":
                        await HandleInfoAsync(bot, chatId, userId, ct);
                        return;

                    case "/cleanip":
                        await HandleCleanIpAsync(bot, chatId, ct);
                        return;

                    case "/uploadips":
                        await HandleUploadIpsHelpAsync(bot, chatId, userId, ct);
                        return;

                    case "/uploadipsadd":
                        await HandleUploadIpsAddHelpAsync(bot, chatId, userId, ct);
                        return;

                    default:
                        await bot.SendMessage(chatId,
                            "<b>Unknown command.</b>\n\nSend /help to see available commands.",
                            parseMode: ParseMode.Html, cancellationToken: ct);
                        return;
                }
            }

            if (_sessionManager.TryGetSession(userId, out var userSession) &&
                userSession.CurrentStep is not ConversationStep.Start and not ConversationStep.Done and not ConversationStep.Error)
            {
                if (userSession.CurrentStep is ConversationStep.AwaitingConfigTemplates)
                {
                    await HandleConfigTemplatesAsync(bot, chatId, userId, text, ct);
                }
                else if (userSession.CurrentStep is ConversationStep.AwaitingIpUploadReplace or ConversationStep.AwaitingIpUploadAppend)
                {
                    await bot.SendMessage(chatId, "Please send a TXT file.", cancellationToken: ct);
                }
                else
                {
                    await _conversationHandler.HandleAsync(bot, chatId, userId, text, ct);
                }
            }
            else
            {
                var fallbackKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Deploy a Worker", "cmd:deployoptions") },
                    new[] { InlineKeyboardButton.WithCallbackData("Get Clean IPs", "cmd:cleanip"), InlineKeyboardButton.WithCallbackData("Generate Configs", "cmd:config") }
                });
                await bot.SendMessage(chatId,
                    "Send /start for info, /deployoptions to deploy a worker, or /help for all commands.",
                    replyMarkup: fallbackKeyboard, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery callback, CancellationToken ct)
    {
        var chatId = callback.Message?.Chat.Id ?? 0;
        var userId = callback.From.Id;
        var data = callback.Data ?? "";

        await bot.AnswerCallbackQuery(callback.Id, cancellationToken: ct);

        if (data.StartsWith("port:"))
        {
            var port = data[5..];
            var matching = _cleanIpCache.Ips
                .Where(ip => ip.Split(':').Last() == port)
                .ToList();

            if (matching.Count == 0)
            {
                await bot.SendMessage(chatId,
                    $"No IPs found with port {port}.",
                    cancellationToken: ct);
                return;
            }

            var rand = new Random();
            var selected = new List<string>();
            int count = Math.Min(10, matching.Count);
            while (selected.Count < count)
            {
                var idx = rand.Next(matching.Count);
                var ip = matching[idx];
                if (!selected.Contains(ip))
                    selected.Add(ip);
            }

            await bot.SendMessage(chatId, string.Join("\n", selected), cancellationToken: ct);
        }
        else if (data == "port:all")
        {
            await HandleCleanIpAsync(bot, chatId, ct);
        }
        else if (data.StartsWith("deploy:"))
        {
            _sessionManager.ResetSession(userId);
            var session = _sessionManager.GetOrCreateSession(userId);
            session.CurrentStep = ConversationStep.DeploymentChoice;
            session.LastActivity = DateTime.UtcNow;
            var choice = data[7..];
            await _conversationHandler.HandleAsync(bot, chatId, userId, choice, ct);
        }
        else if (data.StartsWith("cmd:"))
        {
            var cmd = data[4..];
            switch (cmd)
            {
                case "deployoptions":
                    _sessionManager.ResetSession(userId);
                    var deploySession = _sessionManager.GetOrCreateSession(userId);
                    deploySession.CurrentStep = ConversationStep.DeploymentChoice;
                    deploySession.LastActivity = DateTime.UtcNow;
                    var deployKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[] { InlineKeyboardButton.WithCallbackData("1", "deploy:1"), InlineKeyboardButton.WithCallbackData("2", "deploy:2") },
                        new[] { InlineKeyboardButton.WithCallbackData("3", "deploy:3"), InlineKeyboardButton.WithCallbackData("4", "deploy:4") }
                    });
                    await bot.SendMessage(chatId,
                        "<b>What would you like to deploy?</b>\n\n" +
                        "1 - Edge Tunnel\n2 - BPB Panel\n3 - Nahan\n4 - Yonggekkk",
                        replyMarkup: deployKeyboard, cancellationToken: ct);
                    break;
                case "cleanip":
                    await HandleCleanIpAsync(bot, chatId, ct);
                    break;
                case "config":
                    _sessionManager.ResetSession(userId);
                    var configSession = _sessionManager.GetOrCreateSession(userId);
                    configSession.CurrentStep = ConversationStep.AwaitingConfigTemplates;
                    configSession.LastActivity = DateTime.UtcNow;
                    await bot.SendMessage(chatId,
                        "Send your VLESS/Trojan configs (one per line) or upload a TXT file. " +
                        "I will replace each config's IP:PORT with clean IPs matching its port, " +
                        "rename them sequentially, and send back a TXT file.",
                        cancellationToken: ct);
                    break;
                case "help":
                    await HandleHelpAsync(bot, chatId, ct);
                    break;
            }
        }
    }

    // --- Help ---

    private async Task HandleHelpAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        const string helpMessage = """
            <b>CloudflareWorkerBot - Help</b>

            <b>Deployment:</b>
              /start         - Bot overview and how to use
              /deployoptions - Deploy a new Worker (Edge Tunnel, BPB, etc.)

            <b>Worker Management:</b>
              /list        - List all workers in your account
              /status NAME - Check worker status
              /delete NAME - Delete a worker
              /analytics NAME - View worker analytics

            <b>Clean IP:</b>
              /cleanip - Get 10 random clean Cloudflare IPs by port
              /uploadips - Admin only, upload TXT endpoint list (replaces current list)
              /uploadipsadd - Admin only, upload TXT endpoint list (appends to current list)

            <b>Account:</b>
              /info    - Account overview
              /destroy - Remove all bot-deployed workers + KV
              /status  - Current session status
              /cancel  - Cancel current session
            """;

        await bot.SendMessage(chatId, helpMessage,
            parseMode: ParseMode.Html, cancellationToken: ct);
    }

    // --- Session Status ---

    private async Task HandleSessionStatusAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!_sessionManager.TryGetSession(userId, out var session))
        {
            await bot.SendMessage(chatId,
                "<b>No active session.</b>\n\nSend /start to begin.",
                parseMode: ParseMode.Html, cancellationToken: ct);
            return;
        }

        await bot.SendMessage(chatId,
            $"<b>Session Status</b>\n\n" +
            $"Step: <b>{session.CurrentStep}</b>\n" +
            $"API Token: {(session.ApiToken is not null ? "\u2713" : "\u2717")}\n" +
            $"Account ID: {(session.AccountId is not null ? "\u2713" : "\u2717")}\n" +
            $"Worker Name: {(session.WorkerName is not null ? "\u2713" : "\u2717")}\n" +
            $"Admin Secret: {(session.AdminSecret is not null ? "\u2713" : "\u2717")}\n\n" +
            $"Last activity: {session.LastActivity:HH:mm:ss} UTC",
            parseMode: ParseMode.Html, cancellationToken: ct);
    }

    // --- Worker Management ---

    private async Task HandleListWorkersAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!TryGetCredentials(userId, out var apiToken, out var accountId))
        {
            await bot.SendMessage(chatId, "No active session. Send /start first.",
                cancellationToken: ct);
            return;
        }

        try
        {
            var workers = await _cloudflareApi.ListWorkersAsync(apiToken, accountId, ct);
            if (workers.Count == 0)
            {
                await bot.SendMessage(chatId, "No workers found in this account.",
                    cancellationToken: ct);
                return;
            }

            var list = string.Join("\n", workers.Select(w =>
                $"  \u2022 <code>{w.Id}</code>\n    Modified: {w.ModifiedOn}"));

            await bot.SendMessage(chatId,
                $"<b>Workers ({workers.Count}):</b>\n\n{list}",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list workers for user {UserId}", userId);
            await bot.SendMessage(chatId,
                $"<b>Error:</b> <code>{EscapeHtml(ex.Message)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleWorkerStatusAsync(ITelegramBotClient bot, long chatId, long userId, string workerName, CancellationToken ct)
    {
        if (!TryGetCredentials(userId, out var apiToken, out var accountId))
        {
            await bot.SendMessage(chatId, "No active session. Send /start first.",
                cancellationToken: ct);
            return;
        }

        try
        {
            var settings = await _cloudflareApi.GetWorkerSettingsAsync(apiToken, accountId, workerName, ct);
            var routes = settings.Routes.Count > 0
                ? string.Join("\n", settings.Routes.Select(r => $"  \u2022 {r.Pattern}"))
                : "  (none)";

            await bot.SendMessage(chatId,
                $"<b>Worker: {EscapeHtml(settings.Name)}</b>\n\n" +
                $"ID: <code>{settings.Id}</code>\n" +
                $"workers.dev: <b>{(settings.WorkersDev ? "enabled" : "disabled")}</b>\n" +
                $"Routes:\n{routes}",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get worker status for user {UserId}", userId);
            await bot.SendMessage(chatId,
                $"<b>Error:</b> <code>{EscapeHtml(ex.Message)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleDeleteWorkerAsync(ITelegramBotClient bot, long chatId, long userId, string workerName, CancellationToken ct)
    {
        if (!TryGetCredentials(userId, out var apiToken, out var accountId))
        {
            await bot.SendMessage(chatId, "No active session. Send /start first.",
                cancellationToken: ct);
            return;
        }

        try
        {
            await _cloudflareApi.DeleteWorkerAsync(apiToken, accountId, workerName, ct);
            await bot.SendMessage(chatId,
                $"<b>Worker deleted:</b> <code>{EscapeHtml(workerName)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete worker for user {UserId}", userId);
            await bot.SendMessage(chatId,
                $"<b>Error:</b> <code>{EscapeHtml(ex.Message)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleAnalyticsAsync(ITelegramBotClient bot, long chatId, long userId, string workerName, CancellationToken ct)
    {
        if (!TryGetCredentials(userId, out var apiToken, out var accountId))
        {
            await bot.SendMessage(chatId, "No active session. Send /start first.",
                cancellationToken: ct);
            return;
        }

        try
        {
            var analytics = await _cloudflareApi.GetWorkerAnalyticsAsync(apiToken, accountId, workerName, ct);
            var totalRequests = analytics.Requests.Values.Sum();
            var totalErrors = analytics.Errors.Values.Sum();
            var totalCpu = analytics.CpuTime.Values.Sum();

            await bot.SendMessage(chatId,
                $"<b>Analytics: {EscapeHtml(workerName)}</b>\n\n" +
                $"Requests: <b>{totalRequests:N0}</b>\n" +
                $"Errors: <b>{totalErrors:N0}</b>\n" +
                $"CPU Time: <b>{totalCpu:N0}ms</b>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get analytics for user {UserId}", userId);
            await bot.SendMessage(chatId,
                $"<b>Error:</b> <code>{EscapeHtml(ex.Message)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    // --- Destroy & Info ---

    private async Task HandleDestroyAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!TryGetCredentials(userId, out var apiToken, out var accountId))
        {
            await bot.SendMessage(chatId, "No active session. Send /start first.",
                cancellationToken: ct);
            return;
        }

        try
        {
            var workers = await _cloudflareApi.ListWorkersAsync(apiToken, accountId, ct);

            var deletedWorkers = 0;
            foreach (var w in workers)
            {
                await _cloudflareApi.DeleteWorkerAsync(apiToken, accountId, w.Id, ct);
                deletedWorkers++;
            }

            await bot.SendMessage(chatId,
                $"<b>Destroy complete!</b>\n\n" +
                $"  Deleted {deletedWorkers} worker(s)",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Destroy failed for user {UserId}", userId);
            await bot.SendMessage(chatId,
                $"<b>Error:</b> <code>{EscapeHtml(ex.Message)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    private async Task HandleInfoAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!TryGetCredentials(userId, out var apiToken, out var accountId))
        {
            await bot.SendMessage(chatId, "No active session. Send /start first.",
                cancellationToken: ct);
            return;
        }

        try
        {
            var workers = await _cloudflareApi.ListWorkersAsync(apiToken, accountId, ct);
            var subdomain = await _cloudflareApi.GetWorkersDevSubdomainAsync(apiToken, accountId, ct);

            await bot.SendMessage(chatId,
                $"<b>Account Overview</b>\n\n" +
                $"Account ID: <code>{accountId}</code>\n" +
                $"workers.dev: <code>{(string.IsNullOrEmpty(subdomain) ? "(not set)" : subdomain + ".workers.dev")}</code>\n" +
                $"Workers: <b>{workers.Count}</b>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get info for user {UserId}", userId);
            await bot.SendMessage(chatId,
                $"<b>Error:</b> <code>{EscapeHtml(ex.Message)}</code>",
                parseMode: ParseMode.Html, cancellationToken: ct);
        }
    }

    // --- Clean IP ---

    private async Task HandleCleanIpAsync(ITelegramBotClient bot, long chatId, CancellationToken ct)
    {
        if (_cleanIpCache.Ips.Count == 0)
        {
            await bot.SendMessage(chatId,
                "No clean IPs uploaded yet.",
                cancellationToken: ct);
            return;
        }

        var ports = _cleanIpCache.Ips
            .Select(ip => ip.Split(':').Last())
            .Distinct()
            .OrderBy(p => p.Length)
            .ThenBy(p => p)
            .ToList();

        var rows = new List<InlineKeyboardButton[]>();
        var row = new List<InlineKeyboardButton>();
        foreach (var port in ports)
        {
            row.Add(InlineKeyboardButton.WithCallbackData(port, $"port:{port}"));
            if (row.Count == 3)
            {
                rows.Add(row.ToArray());
                row.Clear();
            }
        }
        if (row.Count > 0)
            rows.Add(row.ToArray());

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("All", "port:all") });

        var keyboard = new InlineKeyboardMarkup(rows);
        await bot.SendMessage(chatId,
            "Select a port to get 10 random IPs:",
            replyMarkup: keyboard, cancellationToken: ct);
    }

    private async Task HandleUploadIpsHelpAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!IsAdmin(userId))
        {
            await bot.SendMessage(chatId, "Admin only command.", cancellationToken: ct);
            return;
        }

        var session = _sessionManager.GetOrCreateSession(userId);
        session.CurrentStep = ConversationStep.AwaitingIpUploadReplace;
        session.LastActivity = DateTime.UtcNow;
        await bot.SendMessage(chatId, "Please send a TXT file with IP:PORT entries to replace the current list.", cancellationToken: ct);
    }

    private async Task HandleUploadIpsAddHelpAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        if (!IsAdmin(userId))
        {
            await bot.SendMessage(chatId, "Admin only command.", cancellationToken: ct);
            return;
        }

        var session = _sessionManager.GetOrCreateSession(userId);
        session.CurrentStep = ConversationStep.AwaitingIpUploadAppend;
        session.LastActivity = DateTime.UtcNow;
        await bot.SendMessage(chatId, "Please send a TXT file with IP:PORT entries to add to the current list.", cancellationToken: ct);
    }

    private async Task HandleDocumentAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        var document = message.Document;
        if (document is null)
            return;

        if (!document.FileName?.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            await bot.SendMessage(message.Chat.Id,
                "Only TXT files are supported.",
                cancellationToken: ct);
            return;
        }

        var session = _sessionManager.GetOrCreateSession(userId);

        // Config templates - any user can upload
        if (session.CurrentStep == ConversationStep.AwaitingConfigTemplates)
        {
            await HandleDocumentConfigAsync(bot, message, userId, ct);
            return;
        }

        // IP upload - admin only
        if (!IsAdmin(userId))
            return;

        try
        {
            if (message.Document is null) return;
            var file = await bot.GetFile(message.Document.FileId, ct);
            if (file is null || string.IsNullOrEmpty(file.FilePath)) return;
            await using var ms = new MemoryStream();
            await bot.DownloadFile(file.FilePath, ms, ct);
            ms.Position = 0;

            using var reader = new StreamReader(ms);
            var content = await reader.ReadToEndAsync(ct);

            var ips = content
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            if (session.CurrentStep == ConversationStep.AwaitingIpUploadReplace)
            {
                _cleanIpCache.Update(ips);
                await bot.SendMessage(message.Chat.Id,
                    $"Uploaded <b>{ips.Count}</b> clean IP endpoints successfully (list replaced).",
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
            else if (session.CurrentStep == ConversationStep.AwaitingIpUploadAppend)
            {
                var current = _cleanIpCache.Ips.ToList();
                var toAdd = ips.Where(ip => !current.Contains(ip)).ToList();
                if (toAdd.Any())
                {
                    current.AddRange(toAdd);
                    _cleanIpCache.Update(current);
                    await bot.SendMessage(message.Chat.Id,
                        $"Added <b>{toAdd.Count}</b> new IP endpoints (total now <b>{current.Count}</b>).",
                        parseMode: ParseMode.Html,
                        cancellationToken: ct);
                }
                else
                {
                    await bot.SendMessage(message.Chat.Id,
                        "No new IP endpoints to add (all already present).",
                        parseMode: ParseMode.Html,
                        cancellationToken: ct);
                }
            }
            else
            {
                _cleanIpCache.Update(ips);
                await bot.SendMessage(message.Chat.Id,
                    $"Uploaded <b>{ips.Count}</b> clean IP endpoints successfully (list replaced).",
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }

            session.CurrentStep = ConversationStep.Start;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload IP list");
            await bot.SendMessage(message.Chat.Id,
                $"Upload failed: {EscapeHtml(ex.Message)}",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    private async Task HandleDocumentConfigAsync(ITelegramBotClient bot, Message message, long userId, CancellationToken ct)
    {
        try
        {
            var file = await bot.GetFile(message.Document!.FileId, ct);
            if (file is null || string.IsNullOrEmpty(file.FilePath)) return;
            await using var ms = new MemoryStream();
            await bot.DownloadFile(file.FilePath, ms, ct);
            ms.Position = 0;

            using var reader = new StreamReader(ms);
            var content = await reader.ReadToEndAsync(ct);

            await HandleConfigTemplatesAsync(bot, message.Chat.Id, userId, content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process config file");
            await bot.SendMessage(message.Chat.Id,
                $"Config processing failed: {EscapeHtml(ex.Message)}",
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    private async Task HandleConfigTemplatesAsync(ITelegramBotClient bot, long chatId, long userId, string content, CancellationToken ct)
    {
        var templates = content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (templates.Count == 0)
        {
            await bot.SendMessage(chatId, "No configs found. Please send valid VLESS/Trojan configs.", cancellationToken: ct);
            return;
        }

        var allResults = new List<string>();
        var configIndex = 1;

        foreach (var template in templates)
        {
            var match = Regex.Match(template, @"@([^:?#]+):(\d+)(?=[?#]|$)");
            if (!match.Success)
            {
                await bot.SendMessage(chatId, $"Could not extract port from config #{configIndex}. Skipping.", cancellationToken: ct);
                configIndex++;
                continue;
            }
            var originalHost = match.Groups[1].Value;
            var port = match.Groups[2].Value;

            var matching = _cleanIpCache.Ips
                .Where(ip => ip.Split(':').Last() == port)
                .ToList();

            if (matching.Count == 0)
            {
                await bot.SendMessage(chatId, $"No clean IPs found for port {port} in config #{configIndex}. Skipping.", cancellationToken: ct);
                configIndex++;
                continue;
            }

            var rand = new Random();
            var selected = new List<string>();
            int count = Math.Min(10, matching.Count);
            while (selected.Count < count)
            {
                var idx = rand.Next(matching.Count);
                var ip = matching[idx];
                if (!selected.Contains(ip))
                    selected.Add(ip);
            }

            foreach (var cleanIp in selected)
            {
                var newConfig = Regex.Replace(template, $@"@{Regex.Escape(originalHost)}:{port}(?=[?#]|$)", $"@{cleanIp}");
                newConfig = Regex.Replace(newConfig, @"#.*$", "") + $"#config-{configIndex}";
                allResults.Add(newConfig);
                configIndex++;
            }
        }

        if (allResults.Count == 0)
        {
            await bot.SendMessage(chatId, "No configs were generated.", cancellationToken: ct);
            return;
        }

        var resultText = string.Join("\n", allResults);
        var bytes = Encoding.UTF8.GetBytes(resultText);
        await using var ms = new MemoryStream(bytes);
        await bot.SendDocument(chatId, new InputFileStream(ms, "configs.txt"),
            caption: $"Generated {allResults.Count} configs.",
            cancellationToken: ct);

        _sessionManager.GetOrCreateSession(userId).CurrentStep = ConversationStep.Start;
    }

    private static bool IsAdmin(long userId)
    {
        var env = Environment.GetEnvironmentVariable("ADMIN_USER_IDS");
        if (string.IsNullOrWhiteSpace(env))
            return false;

        return env
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Any(x => long.TryParse(x, out var id) && id == userId);
    }

    // --- Helpers ---

    private bool TryGetCredentials(long userId, out string apiToken, out string accountId)
    {
        apiToken = "";
        accountId = "";
        if (!_sessionManager.TryGetSession(userId, out var session) ||
            session.ApiToken is null || session.AccountId is null)
            return false;
        apiToken = session.ApiToken;
        accountId = session.AccountId;
        return true;
    }

    private static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
