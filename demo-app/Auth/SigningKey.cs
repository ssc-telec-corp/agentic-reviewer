using Microsoft.IdentityModel.Tokens;

namespace DemoApp.Auth;

public static class SigningKey
{
    public const string EnvironmentVariable = "DEMOAPP_JWT_KEY";

    // The key is a base64-encoded value of at least 32 bytes, injected from the
    // Kubernetes secret in deploy/deployment.yaml. Startup fails without it.
    public static SymmetricSecurityKey Load()
    {
        var encoded = Environment.GetEnvironmentVariable(EnvironmentVariable)
            ?? throw new InvalidOperationException($"{EnvironmentVariable} is not set");
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException($"{EnvironmentVariable} must be at least 32 bytes");
        }
        return new SymmetricSecurityKey(bytes);
    }
}
