using DemoApp.Auth;
using DemoApp.Data;

namespace DemoApp.Endpoints;

public sealed record LoginRequest(string Email, string Password);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        group.MapPost("/login", (LoginRequest request, Store store, PasswordHasher hasher, TokenService tokens, HttpContext http) =>
        {
            var user = store.FindByEmail(request.Email);
            if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }
            var token = tokens.Issue(user);
            http.Response.Cookies.Append(JwtSetup.CookieName, token, JwtSetup.SessionCookieOptions(tokens.Lifetime));
            return Results.Ok(new { token });
        });

        group.MapPost("/logout", (HttpContext http) =>
        {
            http.Response.Cookies.Delete(JwtSetup.CookieName);
            return Results.NoContent();
        });
    }
}
