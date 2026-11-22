using System.Security.Claims;

namespace DemoApp.Auth;

public static class Policies
{
    public const string Admin = "admin";
    public const string Support = "support";

    // There is no fallback policy: an endpoint is anonymous unless its route
    // group calls RequireAuthorization.
    public static IServiceCollection AddAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Admin, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(Support, policy => policy.RequireRole(Roles.Admin, Roles.Support));
        return services;
    }

    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue("sub") ?? throw new InvalidOperationException("missing sub claim"));
}

public static class Roles
{
    public const string Admin = "admin";
    public const string Support = "support";
    public const string User = "user";

    public static readonly string[] All = [Admin, Support, User];
}
