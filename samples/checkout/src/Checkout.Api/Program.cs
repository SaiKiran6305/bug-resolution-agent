using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var orders = new ConcurrentDictionary<string, Order>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/orders/{id}", (string id) => orders.TryGetValue(id, out var order) ? Results.Ok(order) : Results.NotFound());
app.MapPost("/checkout", (CheckoutRequest request) =>
{
    if (request.ProductId != "book" || request.Quantity is < 1 or > 10)
        return Results.BadRequest(new { error = "A book and quantity between 1 and 10 are required." });

    var order = new Order(Guid.NewGuid().ToString("N"), request.ProductId, request.Quantity, "Pending");
    orders[order.Id] = order;
    if (request.PaymentToken != "success")
    {
        orders[order.Id] = order with { Status = "Declined" };
        return Results.Json(new { error = "Payment declined", orderId = order.Id }, statusCode: 402);
    }

    // Payment is a deterministic fake for this test fixture.
    // A production integration would handle idempotency and provider reconciliation.
    // BUG: a successful payment leaves the stored order Pending.
    return Results.Ok(order);
});

app.Run();
public sealed record CheckoutRequest(string ProductId, int Quantity, string PaymentToken);
public sealed record Order(string Id, string ProductId, int Quantity, string Status);
