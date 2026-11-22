using System.Collections.Concurrent;
using DemoApp.Auth;

namespace DemoApp.Data;

public sealed record User(Guid Id, string Email, string DisplayName, string PasswordHash, string Role);

public sealed record Note(Guid Id, Guid OwnerId, string Title, string Body, DateTimeOffset CreatedAt);

// In-memory store. Seed accounts get a password only when DEMOAPP_SEED_PASSWORD
// is set; otherwise their hash matches no password.
public sealed class Store
{
    public ConcurrentDictionary<Guid, User> Users { get; } = new();
    public ConcurrentDictionary<Guid, Note> Notes { get; } = new();

    public Store(PasswordHasher hasher)
    {
        var seedPassword = Environment.GetEnvironmentVariable("DEMOAPP_SEED_PASSWORD");
        var hash = seedPassword is null ? "disabled" : hasher.Hash(seedPassword);
        Add(new User(Guid.NewGuid(), "admin@example.com", "Admin", hash, Roles.Admin));
        Add(new User(Guid.NewGuid(), "support@example.com", "Support", hash, Roles.Support));
        Add(new User(Guid.NewGuid(), "alice@example.com", "Alice", hash, Roles.User));
    }

    public void Add(User user) => Users[user.Id] = user;

    public User? FindByEmail(string email) =>
        Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
}
