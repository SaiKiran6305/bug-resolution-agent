using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

var mode = Environment.GetEnvironmentVariable("BUG_AGENT_TEST_MODE") ?? "suite";
if (mode is not ("suite" or "regression")) throw new ArgumentException("Unknown test mode.");
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
var address = $"http://127.0.0.1:{port}";
var target = typeof(Order).Assembly.Location;
var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
info.ArgumentList.Add(target); info.ArgumentList.Add("--urls"); info.ArgumentList.Add(address);
using var server = Process.Start(info) ?? throw new InvalidOperationException("Cannot start sample API.");
var outputTask = server.StandardOutput.ReadToEndAsync(); var errorTask = server.StandardError.ReadToEndAsync();
using var client = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(5) };
var passed = 0; var failed = 0;
try
{
    var ready = false;
    for (var attempt = 0; attempt < 60; attempt++)
    {
        try { ready = (await client.GetAsync("/health")).IsSuccessStatusCode; if (ready) break; } catch (HttpRequestException) { }
        if (server.HasExited) break;
        await Task.Delay(100);
    }
    if (!ready) { Console.Error.WriteLine("Infrastructure failure: sample API did not start."); return 2; }
    async Task Test(string name, Func<Task> action)
    {
        try { await action(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { failed++; Console.WriteLine($"FAIL: {name}: {ex.Message}"); }
    }
    if (mode == "regression") await Test("Reported checkout regression", () => AgentRegression.RunAsync(client));
    else
    {
        await Test("Invalid quantity is rejected", async () =>
        {
            var response = await client.PostAsJsonAsync("/checkout", new { productId = "book", quantity = 0, paymentToken = "success" });
            if (response.StatusCode != HttpStatusCode.BadRequest) throw new InvalidOperationException("Expected HTTP 400.");
        });
        await Test("Declined payment remains Declined", async () =>
        {
            var response = await client.PostAsJsonAsync("/checkout", new { productId = "book", quantity = 1, paymentToken = "decline" });
            if ((int)response.StatusCode != 402) throw new InvalidOperationException("Expected HTTP 402.");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var order = await client.GetFromJsonAsync<JsonElement>("/orders/" + body.GetProperty("orderId").GetString());
            if (order.GetProperty("status").GetString() != "Declined") throw new InvalidOperationException("Declined order state was lost.");
        });
        await Test("Missing order returns 404", async () =>
        {
            if ((await client.GetAsync("/orders/missing")).StatusCode != HttpStatusCode.NotFound) throw new InvalidOperationException("Expected HTTP 404.");
        });
    }
    Console.WriteLine("BUG_AGENT_RESULT:" + JsonSerializer.Serialize(new { mode, passed, failed }));
    return failed == 0 ? 0 : 1;
}
finally
{
    if (!server.HasExited) server.Kill(true);
    await server.WaitForExitAsync(); await Task.WhenAll(outputTask, errorTask);
}
