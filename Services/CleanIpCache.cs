using System.Collections.Concurrent;

namespace CloudflareWorkerBot.Services;

public sealed class CleanIpCache
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "clean_ips.txt");
    private readonly List<string> _ips = [];
    public IReadOnlyList<string> Ips => _ips.AsReadOnly();
    public DateTime LastUpdated { get; private set; }

    public CleanIpCache()
    {
        Load();
    }

    public void Update(IEnumerable<string> ips)
    {
        _ips.Clear();
        _ips.AddRange(ips.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
        LastUpdated = DateTime.UtcNow;
        File.WriteAllLines(FilePath, _ips);
    }

    private void Load()
    {
        if (!File.Exists(FilePath))
            return;

        _ips.Clear();
        _ips.AddRange(File.ReadAllLines(FilePath).Where(x => !string.IsNullOrWhiteSpace(x)));
        LastUpdated = File.GetLastWriteTimeUtc(FilePath);
    }
}
