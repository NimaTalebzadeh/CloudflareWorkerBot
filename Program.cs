using CloudflareWorkerBot.Cloudflare;
using CloudflareWorkerBot.Services;
using CloudflareWorkerBot.State;
using CloudflareWorkerBot.Telegram;
using Serilog;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:" + (Environment.GetEnvironmentVariable("PORT") ?? "5000"));

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();



builder.Services.AddSingleton<UserSessionManager>();
builder.Services.AddHttpClient<CloudflareApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.cloudflare.com");
});
builder.Services.AddSingleton<ConversationHandler>();
builder.Services.AddSingleton<BotUpdateRouter>();

builder.Services.AddSingleton<CleanIpCache>();

builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    // First try TELEGRAM_BOTTOKEN env var (Railway) then fallback to config key
    var token = Environment.GetEnvironmentVariable("TELEGRAM_BOTTOKEN")
                ?? builder.Configuration["Telegram:BotToken"];
    if (string.IsNullOrWhiteSpace(token))
    {
        throw new InvalidOperationException("Telegram bot token is not configured. Set TELEGRAM_BOTTOKEN env var or Telegram:BotToken in config.");
    }
    return new TelegramBotClient(token);
});

var app = builder.Build();

app.UseSerilogRequestLogging();

var botClient = app.Services.GetRequiredService<ITelegramBotClient>();

var cts = new CancellationTokenSource();

_ = Task.Run(async () =>
{
    var me = await botClient.GetMe(cts.Token);
    Log.Information("Bot started as @{Username}", me.Username);

    var offset = 0;
    while (!cts.Token.IsCancellationRequested)
    {
        try
        {
            var updates = await botClient.GetUpdates(
                offset: offset,
                timeout: 30,
                cancellationToken: cts.Token);

            foreach (var update in updates)
            {
                offset = update.Id + 1;
                _ = Task.Run(async () =>
                {
var router = app.Services.GetRequiredService<BotUpdateRouter>();
                     var scopedBot = app.Services.GetRequiredService<ITelegramBotClient>();
                     await router.HandleAsync(scopedBot, update, cts.Token);
                });
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in polling loop");
            await Task.Delay(5000, cts.Token);
        }
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
    Log.Information("Bot shutting down...");
});

app.Map("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();


