using DemoApp.Auth;
using DemoApp.Data;

namespace DemoApp.Endpoints;

public sealed record UserSummaryDto(Guid Id, string Email, string DisplayName, string Role);

public sealed record SetRoleRequest(string Role);

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(Policies.Admin);

        group.MapGet("/users", (Store store) =>
            store.Users.Values.Select(u => new UserSummaryDto(u.Id, u.Email, u.DisplayName, u.Role)));

        group.MapPost("/users/{id:guid}/role", (Guid id, SetRoleRequest request, Store store) =>
        {
            if (!Roles.All.Contains(request.Role) || !store.Users.TryGetValue(id, out var user))
            {
                return Results.BadRequest();
            }
            store.Users[id] = user with { Role = request.Role };
            return Results.NoContent();
        });
    }
}
