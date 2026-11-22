using System.Security.Claims;
using DemoApp.Data;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DemoApp.Auth;

public sealed class TokenService(SymmetricSecurityKey key, IConfiguration config)
{
    private readonly JsonWebTokenHandler _handler = new();

    public TimeSpan Lifetime => TimeSpan.FromMinutes(config.GetValue("Jwt:LifetimeMinutes", 30));

    public string Issue(User user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Expires = DateTime.UtcNow.Add(Lifetime),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("role", user.Role),
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };
        return _handler.CreateToken(descriptor);
    }
}
