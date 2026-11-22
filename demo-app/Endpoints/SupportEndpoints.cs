using System.Security.Claims;
using DemoApp.Auth;
using DemoApp.Data;

namespace DemoApp.Endpoints;

public sealed record ResetPasswordRequest(string NewPassword);

public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/support").RequireAuthorization(Policies.Support);

        // Lets the support desk set a temporary password when a customer is locked out.
        group.MapPost("/users/{id:guid}/reset-password", (Guid id, ResetPasswordRequest request, ClaimsPrincipal principal, Store store, PasswordHasher hasher) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
            {
                return Results.BadRequest();
            }
            if (!store.Users.TryGetValue(id, out var user))
            {
                return Results.NotFound();
            }
            // Support may reset only customer accounts; staff accounts need an admin.
            if (user.Role != Roles.User && !principal.IsInRole(Roles.Admin))
            {
                return Results.Forbid();
            }
            store.Users[id] = user with { PasswordHash = hasher.Hash(request.NewPassword) };
            return Results.NoContent();
        });
    }
}
