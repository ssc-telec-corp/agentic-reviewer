using System.Text.RegularExpressions;

namespace DemoApp.Net;

public sealed class OutboundOptions
{
    public string[] AllowedHosts { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 5;
    public int MaxResponseBytes { get; set; } = 262_144;
}

// The only component allowed to make HTTP requests to URLs that users supply.
// It accepts https URLs whose host exactly matches Network:Outbound:AllowedHosts
// and never follows redirects.
public sealed partial class OutboundClient(HttpClient http, OutboundOptions options)
{
    public bool IsAllowed(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && options.AllowedHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase);

    public async Task<string?> FetchTitleAsync(Uri uri, CancellationToken ct)
    {
        if (!IsAllowed(uri))
        {
            return null;
        }
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[options.MaxResponseBytes];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        var html = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        var match = TitleRegex().Match(html);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex("<title[^>]*>([^<]{1,300})</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRegex();
}

public static class OutboundSetup
{
    public static IServiceCollection AddOutboundClient(this IServiceCollection services, IConfiguration config)
    {
        var options = config.GetSection("Network:Outbound").Get<OutboundOptions>() ?? new OutboundOptions();
        services.AddSingleton(options);
        services.AddHttpClient<OutboundClient>(client => client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        return services;
    }
}
