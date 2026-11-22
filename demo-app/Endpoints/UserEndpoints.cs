using System.Security.Claims;
using DemoApp.Auth;
using DemoApp.Data;

namespace DemoApp.Endpoints;

public sealed record MyProfileDto(Guid Id, string Email, string DisplayName, string Role);

// Shown to other users, for example next to a shared note. Contains no contact data.
public sealed record PublicProfileDto(Guid Id, string DisplayName);

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal principal, Store store) =>
            store.Users.TryGetValue(principal.UserId(), out var user)
                ? Results.Ok(new MyProfileDto(user.Id, user.Email, user.DisplayName, user.Role))
                : Results.NotFound());

        group.MapGet("/{id:guid}", (Guid id, Store store) =>
            store.Users.TryGetValue(id, out var user)
                ? Results.Ok(new PublicProfileDto(user.Id, user.DisplayName))
                : Results.NotFound());
    }
}
