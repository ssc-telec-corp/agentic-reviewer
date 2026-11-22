using System.Security.Claims;
using DemoApp.Auth;
using DemoApp.Data;

namespace DemoApp.Endpoints;

public sealed record NoteDto(Guid Id, string Title, string Body, DateTimeOffset CreatedAt);

public sealed record CreateNoteRequest(string Title, string Body);

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notes").RequireAuthorization();

        group.MapGet("/", (ClaimsPrincipal principal, Store store) =>
        {
            var userId = principal.UserId();
            return store.Notes.Values
                .Where(n => n.OwnerId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(ToDto);
        });

        // Notes owned by someone else return 404, not 403, so ids cannot be probed.
        group.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal principal, Store store) =>
            store.Notes.TryGetValue(id, out var note) && note.OwnerId == principal.UserId()
                ? Results.Ok(ToDto(note))
                : Results.NotFound());

        group.MapPost("/", (CreateNoteRequest request, ClaimsPrincipal principal, Store store) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || request.Body.Length > 10_000)
            {
                return Results.BadRequest();
            }
            var note = new Note(Guid.NewGuid(), principal.UserId(), request.Title, request.Body, DateTimeOffset.UtcNow);
            store.Notes[note.Id] = note;
            return Results.Created($"/api/notes/{note.Id}", ToDto(note));
        });

        group.MapDelete("/{id:guid}", (Guid id, ClaimsPrincipal principal, Store store) =>
        {
            if (!store.Notes.TryGetValue(id, out var note) || note.OwnerId != principal.UserId())
            {
                return Results.NotFound();
            }
            store.Notes.TryRemove(id, out _);
            return Results.NoContent();
        });
    }

    private static NoteDto ToDto(Note note) => new(note.Id, note.Title, note.Body, note.CreatedAt);
}
