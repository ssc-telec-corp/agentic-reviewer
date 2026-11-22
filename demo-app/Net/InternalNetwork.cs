using System.Net;
using DemoApp.Data;
using Microsoft.AspNetCore.HttpOverrides;

namespace DemoApp.Net;

public static class InternalNetwork
{
    // X-Forwarded-For is honoured only from the load balancer addresses in
    // Network:ForwardedHeaders:KnownProxies. RemoteIpAddress is therefore the
    // real client address, which /internal/* relies on.
    public static IServiceCollection AddTrustedProxies(this IServiceCollection services, IConfiguration config)
    {
        var proxies = config.GetSection("Network:ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }
        });
        return services;
    }

    public static void MapInternalEndpoints(this WebApplication app)
    {
        var networks = (app.Configuration.GetSection("Network:InternalNetworks").Get<string[]>() ?? [])
            .Select(System.Net.IPNetwork.Parse)
            .ToArray();

        var group = app.MapGroup("/internal").AddEndpointFilter(async (context, next) =>
        {
            var remote = context.HttpContext.Connection.RemoteIpAddress;
            if (remote is null || !networks.Any(n => n.Contains(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote)))
            {
                return Results.NotFound();
            }
            return await next(context);
        });

        group.MapGet("/stats", (Store store) => new { users = store.Users.Count, notes = store.Notes.Count });
    }
}
