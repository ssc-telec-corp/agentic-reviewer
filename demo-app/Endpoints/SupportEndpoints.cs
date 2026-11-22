using DemoApp.Auth;
using DemoApp.Data;

namespace DemoApp.Endpoints;

public sealed record ResetPasswordRequest(string NewPassword);

public static class SupportEndpoints
{
    public static void MapSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/support").RequireAuthorization();

        // Lets the support desk set a temporary password when a customer is locked out.
        group.MapPost("/users/{id:guid}/reset-password", (Guid id, ResetPasswordRequest request, Store store, PasswordHasher hasher) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 12)
            {
                return Results.BadRequest();
            }
            if (!store.Users.TryGetValue(id, out var user))
            {
                return Results.NotFound();
            }
            store.Users[id] = user with { PasswordHash = hasher.Hash(request.NewPassword) };
            return Results.NoContent();
        });
    }
}
