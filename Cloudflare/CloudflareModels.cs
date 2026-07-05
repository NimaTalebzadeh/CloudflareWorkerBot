using System.Text.Json.Serialization;

namespace CloudflareWorkerBot.Cloudflare;

public sealed class CloudflareApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errors")]
    public List<CloudflareApiError> Errors { get; set; } = [];

    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

public sealed class CloudflareApiError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public sealed class CloudflareApiListResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errors")]
    public List<CloudflareApiError> Errors { get; set; } = [];

    [JsonPropertyName("result")]
    public List<T> Result { get; set; } = [];
}

public sealed class KvNamespaceResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}

public sealed class SecretResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class AccountResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public sealed class WorkerScriptResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("modified_on")]
    public string ModifiedOn { get; set; } = "";

    [JsonPropertyName("created_on")]
    public string CreatedOn { get; set; } = "";

    [JsonPropertyName("compatibility_date")]
    public string CompatibilityDate { get; set; } = "";
}

public sealed class WorkerSettingsResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("routes")]
    public List<WorkerRoute> Routes { get; set; } = [];

    [JsonPropertyName("workers_dev")]
    public bool WorkersDev { get; set; }
}

public sealed class WorkerRoute
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    [JsonPropertyName("script")]
    public string Script { get; set; } = "";

    [JsonPropertyName("zone_id")]
    public string ZoneId { get; set; } = "";
}

public sealed class WorkerAnalyticsResult
{
    [JsonPropertyName("requests")]
    public Dictionary<string, long> Requests { get; set; } = [];

    [JsonPropertyName("errors")]
    public Dictionary<string, long> Errors { get; set; } = [];

    [JsonPropertyName("subrequests")]
    public Dictionary<string, long> Subrequests { get; set; } = [];

    [JsonPropertyName("cpu_time")]
    public Dictionary<string, long> CpuTime { get; set; } = [];

    [JsonPropertyName("wall_time")]
    public Dictionary<string, long> WallTime { get; set; } = [];
}

public sealed class D1DatabaseResult
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";
}
