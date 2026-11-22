using DemoApp.Net;

namespace DemoApp.Endpoints;

public sealed record LinkPreviewRequest(string Url);

public static class LinkPreviewEndpoints
{
    public static void MapLinkPreviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/link-preview").RequireAuthorization();

        group.MapPost("/", async (LinkPreviewRequest request, OutboundClient client, CancellationToken ct) =>
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            {
                return Results.BadRequest();
            }
            var title = await client.FetchTitleAsync(uri, ct);
            return title is null ? Results.NotFound() : Results.Ok(new { title });
        });
    }
}
