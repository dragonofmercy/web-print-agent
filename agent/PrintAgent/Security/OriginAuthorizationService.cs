using PrintAgent.Storage;

namespace PrintAgent.Security;

public enum OriginClassification { Rejected, Unknown, Allowed }

public sealed class OriginAuthorizationService
{
    private readonly ConfigStore _configStore;
    private readonly bool _allowInsecureOrigins;

    public OriginAuthorizationService(ConfigStore configStore, bool allowInsecureOrigins)
    {
        _configStore = configStore;
        _allowInsecureOrigins = allowInsecureOrigins;
    }

    public OriginClassification Classify(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return OriginClassification.Rejected;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return OriginClassification.Rejected;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return OriginClassification.Rejected;
        if (uri.Scheme == Uri.UriSchemeHttp && !_allowInsecureOrigins) return OriginClassification.Rejected;

        var normalized = NormalizeOrigin(uri);
        return _configStore.IsOriginAllowed(normalized) ? OriginClassification.Allowed : OriginClassification.Unknown;
    }

    public string Normalize(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return origin;
        return NormalizeOrigin(uri);
    }

    private static string NormalizeOrigin(Uri uri)
    {
        // Scheme + host + port (explicit if non-default)
        var defaultPort = uri.Scheme == Uri.UriSchemeHttps ? 443 : 80;
        return uri.Port == defaultPort
            ? $"{uri.Scheme}://{uri.Host}"
            : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }
}
