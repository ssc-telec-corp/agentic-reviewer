using DemoApp.Auth;
using DemoApp.Data;
using DemoApp.Endpoints;
using DemoApp.Net;
using DemoApp.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddJwtAuth(builder.Configuration);
builder.Services.AddAppAuthorization();
builder.Services.AddOutboundClient(builder.Configuration);
builder.Services.AddTrustedProxies(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Network:Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .AllowCredentials()));

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapNoteEndpoints();
app.MapAdminEndpoints();
app.MapSupportEndpoints();
app.MapLinkPreviewEndpoints();
app.MapInternalEndpoints();
app.MapGet("/healthz", () => Results.Ok("ok"));

app.Run();
