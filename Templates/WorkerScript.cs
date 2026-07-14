using CloudflareWorkerBot.State;

namespace CloudflareWorkerBot.Templates;

public static class WorkerScript
{
    public static string GetScript(DeploymentType type)
    {
        var fileName = type switch
        {
            DeploymentType.Bpb => "bpb.js",
            DeploymentType.Nahan => "nahan.js",
            DeploymentType.Yonggekkk => "yonggekkk.js",
            DeploymentType.Cfnew => "cfnew.js",
            _ => "edge_tunnel.js"
        };
        var path = Path.Combine(AppContext.BaseDirectory, "Workers", fileName);
        if (!File.Exists(path))
            path = Path.Combine(Directory.GetCurrentDirectory(), "Workers", fileName);

        return File.ReadAllText(path);
    }

    public static string GetMetadataJson(DeploymentType type, string? resourceId = null)
    {
        if (type == DeploymentType.Yonggekkk || type == DeploymentType.Cfnew)
        {
            return $$"""
                {
                  "main_module": "worker.js",
                  "compatibility_date": "{{DateTime.UtcNow:yyyy-MM-dd}}",
                  "bindings":[]
                }
                """;
        }

        if (type == DeploymentType.Nahan)
        {
            var d1Binding = resourceId is not null
                ? $"\"bindings\":[{{\"name\":\"IOT_DB\",\"type\":\"d1\",\"database_id\":\"{resourceId}\"}}]"
                : "\"bindings\":[]";

            return $$"""
                {
                  "main_module": "worker.js",
                  "compatibility_date": "{{DateTime.UtcNow:yyyy-MM-dd}}",
                  {{d1Binding}}
                }
                """;
        }

        var bindingName = type == DeploymentType.Bpb ? "kv" : "KV";
        var kvBinding = resourceId is not null
            ? $"\"bindings\":[{{\"name\":\"{bindingName}\",\"type\":\"kv_namespace\",\"namespace_id\":\"{resourceId}\"}}]"
            : "\"bindings\":[]";

        return $$"""
            {
              "main_module": "worker.js",
              "compatibility_date": "{{DateTime.UtcNow:yyyy-MM-dd}}",
              {{kvBinding}}
            }
            """;
    }

    public static string GenerateUuid()
    {
        var bytes = new byte[16];
        Random.Shared.NextBytes(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return Convert.ToHexString(bytes).ToLowerInvariant()
            .Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-");
    }

    public static string GenerateTrPass()
    {
        var bytes = new byte[32];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateSubPath()
    {
        var bytes = new byte[16];
        Random.Shared.NextBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
