using BugResolution.Api;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
Directory.SetCurrentDirectory(builder.Environment.ContentRootPath);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 100_000);
builder.Services.AddSingleton<Store>(); builder.Services.AddSingleton<Repositories>();
builder.Services.AddSingleton<ModelProvider>(); builder.Services.AddSingleton<Verification>(); builder.Services.AddSingleton<GitHub>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHttpClient("model", client => client.Timeout = TimeSpan.FromSeconds(180));
builder.Services.AddHttpClient("github", client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("writes", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
var key = builder.Configuration["Agent:ApiKey"] ?? "";
if (!app.Environment.IsDevelopment() && key.Length < 24) throw new InvalidOperationException("Set Agent__ApiKey to a random value of at least 24 characters outside Development.");
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.Headers.CacheControl = "no-store";
        var provided = context.Request.Headers["X-Api-Key"].ToString();
        var authenticated = key.Length > 0 && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(provided)));
        var localDevelopment = key.Length == 0 && app.Environment.IsDevelopment() && context.Connection.RemoteIpAddress is not null && IPAddress.IsLoopback(context.Connection.RemoteIpAddress);
        if (!authenticated && !localDevelopment) { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { error = "Enter the configured application access key." }); return; }
        if (localDevelopment && context.Request.Headers.TryGetValue("Origin", out var origin) && Uri.TryCreate(origin, UriKind.Absolute, out var originUri) && originUri.Host is not ("localhost" or "127.0.0.1" or "[::1]"))
        { context.Response.StatusCode = 403; return; }
    }
    try { await next(context); }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
    {
        context.Response.StatusCode = ex is KeyNotFoundException ? 404 : ex is ArgumentException ? 400 : 409;
        await context.Response.WriteAsJsonAsync(new { error = Safety.Redact(ex is KeyNotFoundException ? "Investigation not found." : ex.Message) });
    }
});
app.UseRateLimiter(); app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/configuration", (Repositories repos, ModelProvider model, Verification runner, GitHub github) => Results.Ok(new
{
    modelConfigured = model.Configured, runnerEnabled = runner.Enabled, githubConfigured = github.Configured,
    services = repos.Services.Select(s => new { s.Id, s.Name, s.Bundled, s.GitHubRepository })
}));
app.MapGet("/api/investigations", (Store store) => Results.Ok(store.List().Select(r => new { r.Id, r.CreatedAt, r.Status, r.Verification, r.Report.Title, r.Report.ServiceId, r.Report.Demo })));
app.MapGet("/api/investigations/{id}", (string id, Store store) => store.Get(id) is { } run ? Results.Ok(run) : Results.NotFound());
app.MapPost("/api/investigations", (BugReport report, Store store, Repositories repos) =>
{
    report = Safety.Validate(report); var service = repos.Get(report.ServiceId);
    if (report.Demo && (service.Id != "checkout-demo" || !service.Bundled)) throw new ArgumentException("Demo mode is only available for the bundled checkout service.");
    if (store.List(1000).Count(r => r.Status is "Queued" or "Running") >= 10) return Results.Problem("The investigation queue is full.", statusCode: 429);
    var run = new Investigation { Report = report }; store.Save(run); return Results.Accepted($"/api/investigations/{run.Id}", run);
}).RequireRateLimiting("writes");
app.MapPost("/api/issues/import", async (ImportRequest request, GitHub github, CancellationToken ct) => Results.Ok(await github.Import(request, ct))).RequireRateLimiting("writes");
app.MapPost("/api/investigations/{id}/review", (string id, ReviewRequest request, Store store) => Results.Ok(store.Review(id, request))).RequireRateLimiting("writes");
app.MapPost("/api/investigations/{id}/pull-request", async (string id, GitHub github, CancellationToken ct) => Results.Ok(await github.Publish(id, ct))).RequireRateLimiting("writes");
app.MapGet("/api/investigations/{id}/patch", (string id, Store store) => store.Get(id) is { Proposal: not null } run ? Results.File(Encoding.UTF8.GetBytes(run.Diff), "text/x-diff", $"{id}.patch") : Results.NotFound());
app.MapFallback((HttpContext context) => context.Request.Path.StartsWithSegments("/api") ? Results.NotFound() : Results.File(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html"), "text/html"));
app.Run();
public partial class Program;
