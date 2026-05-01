using System.Text.Json;

namespace PrintAgent.Storage;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly object _lock = new();

    public ConfigStore(string path) => _path = path;

    public ConfigModel Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_path)) return new ConfigModel();
            try
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<ConfigModel>(json) ?? new ConfigModel();
            }
            catch (JsonException)
            {
                return new ConfigModel();
            }
        }
    }

    public bool AddAllowedOrigin(string origin)
    {
        lock (_lock)
        {
            var model = Load();
            if (model.AllowedOrigins.Contains(origin, StringComparer.Ordinal)) return false;
            model.AllowedOrigins.Add(origin);
            Save(model);
            return true;
        }
    }

    public bool IsOriginAllowed(string origin)
    {
        lock (_lock)
        {
            return Load().AllowedOrigins.Contains(origin, StringComparer.Ordinal);
        }
    }

    public IReadOnlyList<string> GetAllowedOrigins()
    {
        lock (_lock)
        {
            return Load().AllowedOrigins.ToList();
        }
    }

    public bool RemoveAllowedOrigin(string origin)
    {
        lock (_lock)
        {
            var model = Load();
            if (!model.AllowedOrigins.Remove(origin)) return false;
            Save(model);
            return true;
        }
    }

    public int RemoveAllowedOrigins(IEnumerable<string> origins)
    {
        lock (_lock)
        {
            var model = Load();
            var removed = 0;
            foreach (var origin in origins)
                if (model.AllowedOrigins.Remove(origin)) removed++;
            if (removed > 0) Save(model);
            return removed;
        }
    }

    public int ClearAllowedOrigins()
    {
        lock (_lock)
        {
            var model = Load();
            var count = model.AllowedOrigins.Count;
            if (count == 0) return 0;
            model.AllowedOrigins.Clear();
            Save(model);
            return count;
        }
    }

    public void SetLastBoundPort(int port)
    {
        lock (_lock)
        {
            var model = Load();
            model.LastBoundPort = port;
            Save(model);
        }
    }

    public void SetCertThumbprint(string thumbprint)
    {
        lock (_lock)
        {
            var model = Load();
            model.CertThumbprint = thumbprint;
            Save(model);
        }
    }

    public string? GetCertThumbprint()
    {
        lock (_lock)
        {
            return Load().CertThumbprint;
        }
    }

    private void Save(ConfigModel model)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(model, JsonOptions));
    }
}
