using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CloudflareWorkerBot.Cloudflare;

public sealed class CloudflareApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudflareApiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public CloudflareApiService(HttpClient httpClient, ILogger<CloudflareApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task DeployWorkerAsync(
        string apiToken, string accountId, string scriptName,
        string scriptContent, string? metadataJson, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/client/v4/accounts/{accountId}/workers/scripts/{scriptName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        using var content = new MultipartFormDataContent();
        var scriptPart = new ByteArrayContent(Encoding.UTF8.GetBytes(scriptContent));
        scriptPart.Headers.ContentType = new MediaTypeHeaderValue("application/javascript+module");
        content.Add(scriptPart, "worker.js", "worker.js");

        if (!string.IsNullOrEmpty(metadataJson))
        {
            var metaPart = new ByteArrayContent(Encoding.UTF8.GetBytes(metadataJson));
            metaPart.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            content.Add(metaPart, "metadata", "metadata");
        }
        request.Content = content;

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("DeployWorker {ScriptName} -> {Status}", scriptName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to deploy worker: {errors}");
        }
    }

    public async Task<string> CreateKvNamespaceAsync(
        string apiToken, string accountId, string title, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/client/v4/accounts/{accountId}/storage/kv/namespaces");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { title }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("CreateKvNamespace '{Title}' -> {Status}", title, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<KvNamespaceResult>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success || apiResponse.Result is null)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to create KV namespace: {errors}");
        }
        return apiResponse.Result.Id;
    }

    public async Task EnableWorkersDevAsync(
        string apiToken, string accountId, string scriptName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/client/v4/accounts/{accountId}/workers/workers/{scriptName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                name = scriptName,
                subdomain = new { enabled = true }
            }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("EnableWorkersDev {ScriptName} -> {Status}", scriptName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            _logger.LogWarning("Failed to enable workers.dev: {Errors}", errors);
        }
    }

    public async Task<string> GetWorkersDevSubdomainAsync(
        string apiToken, string accountId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/client/v4/accounts/{accountId}/workers/subdomain");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("GetWorkersDevSubdomain -> {Status}", response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("result", out var result) &&
            result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("subdomain", out var subdomain))
        {
            return subdomain.GetString() ?? "";
        }
        return "";
    }

    public async Task<string> ClaimSubdomainAsync(
        string apiToken, string accountId, string subdomain, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/client/v4/accounts/{accountId}/workers/subdomain");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { subdomain }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("ClaimSubdomain '{Subdomain}' -> {Status}", subdomain, response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("result", out var result) &&
            result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("subdomain", out var claimedSubdomain))
        {
            return claimedSubdomain.GetString() ?? "";
        }
        return "";
    }

    public async Task<List<AccountResult>> GetAccountsAsync(string apiToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client/v4/accounts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<List<AccountResult>>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success || apiResponse.Result is null)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to fetch accounts: {errors}");
        }
        return apiResponse.Result;
    }

    // --- Worker Management ---

    public async Task<List<WorkerScriptResult>> ListWorkersAsync(
        string apiToken, string accountId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/client/v4/accounts/{accountId}/workers/scripts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiListResponse<WorkerScriptResult>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to list workers: {errors}");
        }
        return apiResponse.Result;
    }

    public async Task<WorkerSettingsResult> GetWorkerSettingsAsync(
        string apiToken, string accountId, string scriptName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/client/v4/accounts/{accountId}/workers/scripts/{scriptName}/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<WorkerSettingsResult>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success || apiResponse.Result is null)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to get worker settings: {errors}");
        }
        return apiResponse.Result;
    }

    public async Task DeleteWorkerAsync(
        string apiToken, string accountId, string scriptName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"/client/v4/accounts/{accountId}/workers/scripts/{scriptName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("DeleteWorker {ScriptName} -> {Status}", scriptName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to delete worker: {errors}");
        }
    }

    // --- Analytics ---

    public async Task<WorkerAnalyticsResult> GetWorkerAnalyticsAsync(
        string apiToken, string accountId, string scriptName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/client/v4/accounts/{accountId}/workers/scripts/{scriptName}/analytics");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("GetWorkerAnalytics {ScriptName} -> {Status}", scriptName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<WorkerAnalyticsResult>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success || apiResponse.Result is null)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to get analytics: {errors}");
        }
        return apiResponse.Result;
    }

    // --- D1 Database ---

    public async Task<string> CreateD1DatabaseAsync(
        string apiToken, string accountId, string name, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/client/v4/accounts/{accountId}/d1/database");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { name }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("CreateD1Database '{Name}' -> {Status}", name, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<D1DatabaseResult>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success || apiResponse.Result is null)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to create D1 database: {errors}");
        }
        return apiResponse.Result.Uuid;
    }

    // --- Pages API ---

    public async Task CreatePagesProjectAsync(
        string apiToken, string accountId, string projectName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/client/v4/accounts/{accountId}/pages/projects");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                name = projectName,
                production_branch = "main"
            }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("CreatePagesProject '{Name}' -> {Status}", projectName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            // 409 Conflict means project already exists — that's fine
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return;

            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to create Pages project: {errors}");
        }
    }

    public async Task UploadPagesDeploymentAsync(
        string apiToken, string accountId, string projectName,
        string scriptContent, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/client/v4/accounts/{accountId}/pages/projects/{projectName}/deployments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);

        // Hash the script content for the manifest
        var hashBytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(scriptContent));
        var scriptHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var manifest = JsonSerializer.Serialize(new
        {
            _worker_js = scriptHash
        });

        using var content = new MultipartFormDataContent();
        var jsPart = new ByteArrayContent(Encoding.UTF8.GetBytes(scriptContent));
        jsPart.Headers.ContentType = new MediaTypeHeaderValue("application/javascript");
        content.Add(jsPart, "_worker.js", "_worker.js");

        var manifestPart = new StringContent(manifest, Encoding.UTF8, "application/json");
        content.Add(manifestPart, "manifest");
        request.Content = content;

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("UploadPagesDeployment '{Name}' -> {Status}", projectName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to upload Pages deployment: {errors}");
        }
    }

    public async Task<string> ConfigurePagesProjectAsync(
        string apiToken, string accountId, string projectName,
        string uuid, CancellationToken ct)
    {
        // First create KV namespace
        var kvName = $"kv-{Guid.NewGuid().ToString("N")[..8]}";
        using var kvRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/client/v4/accounts/{accountId}/storage/kv/namespaces");
        kvRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        kvRequest.Content = new StringContent(
            JsonSerializer.Serialize(new { title = kvName }, JsonOptions), Encoding.UTF8, "application/json");

        var kvResponse = await _httpClient.SendAsync(kvRequest, ct);
        var kvBody = await kvResponse.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("CreateKvNamespace '{Name}' -> {Status}", kvName, kvResponse.StatusCode);

        var kvApiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<KvNamespaceResult>>(kvBody, JsonOptions);
        if (kvApiResponse is null || !kvApiResponse.Success || kvApiResponse.Result is null)
        {
            var errors = string.Join(", ", kvApiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to create KV namespace: {errors}");
        }
        var newKvId = kvApiResponse.Result.Id;

        // Then do a single PATCH with both KV binding + env_var 'u' in one shot
        using var patchRequest = new HttpRequestMessage(HttpMethod.Patch,
            $"/client/v4/accounts/{accountId}/pages/projects/{projectName}");
        patchRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        patchRequest.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                deployment_configs = new
                {
                    preview = new
                    {
                        kv_namespaces = new Dictionary<string, object>
                        {
                            ["C"] = new { namespace_id = newKvId }
                        },
                        env_vars = new Dictionary<string, object>
                        {
                            ["u"] = new { value = uuid }
                        }
                    },
                    production = new
                    {
                        kv_namespaces = new Dictionary<string, object>
                        {
                            ["C"] = new { namespace_id = newKvId }
                        },
                        env_vars = new Dictionary<string, object>
                        {
                            ["u"] = new { value = uuid }
                        }
                    }
                }
            }, JsonOptions), Encoding.UTF8, "application/json");

        var patchResponse = await _httpClient.SendAsync(patchRequest, ct);
        var patchBody = await patchResponse.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("ConfigurePagesProject '{Name}' -> {Status}", projectName, patchResponse.StatusCode);

        var patchApiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(patchBody, JsonOptions);
        if (patchApiResponse is null || !patchApiResponse.Success)
        {
            var errors = string.Join(", ", patchApiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to configure Pages project (KV+uuid): {errors}");
        }

        return newKvId;
    }

    public async Task SetPagesSecretAsync(
        string apiToken, string accountId, string projectName,
        string secretName, string secretValue, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"/client/v4/accounts/{accountId}/pages/projects/{projectName}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                deployment_configs = new
                {
                    preview = new
                    {
                        env_vars = new Dictionary<string, object>
                        {
                            [secretName] = new { value = secretValue, type = "secret" }
                        }
                    },
                    production = new
                    {
                        env_vars = new Dictionary<string, object>
                        {
                            [secretName] = new { value = secretValue, type = "secret" }
                        }
                    }
                }
            }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogInformation("SetPagesSecret '{Name}/{Project}' -> {Status}", projectName, secretName, response.StatusCode);

        var apiResponse = JsonSerializer.Deserialize<CloudflareApiResponse<object>>(body, JsonOptions);
        if (apiResponse is null || !apiResponse.Success)
        {
            var errors = string.Join(", ", apiResponse?.Errors.Select(e => e.Message) ?? ["Unknown error"]);
            throw new InvalidOperationException($"Failed to set Pages secret: {errors}");
        }
    }

    public async Task SetSecretViaWranglerAsync(
        string apiToken, string accountId, string scriptName,
        string secretName, string secretValue, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"echo '{secretValue}' | npx wrangler secret put {secretName} --name {scriptName}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.Environment["CLOUDFLARE_API_TOKEN"] = apiToken;
        psi.Environment["CLOUDFLARE_ACCOUNT_ID"] = accountId;

        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        _logger.LogInformation("Wrangler secret put -> ExitCode: {Code}, Output: {Output}", process.ExitCode, stdout);
        if (process.ExitCode != 0 && !string.IsNullOrEmpty(stderr))
            _logger.LogWarning("Wrangler secret stderr: {Stderr}", stderr);
    }
}
