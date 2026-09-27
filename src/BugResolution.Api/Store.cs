using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace BugResolution.Api;

// One API/worker process per database. SQLite serializes durable state writes.
public sealed class Store
{
    private readonly string connectionString;
    private readonly object gate = new();
    public Store(IConfiguration config)
    {
        var directory = Path.GetFullPath(config["DataDirectory"] ?? "data");
        Directory.CreateDirectory(directory);
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "agent.db") }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY, created TEXT NOT NULL, status TEXT NOT NULL, body TEXT NOT NULL);";
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public void Save(Investigation run)
    {
        lock (gate)
        {
            using var db = Open(); using var command = db.CreateCommand();
            command.CommandText = "INSERT INTO runs VALUES($id,$created,$status,$body) ON CONFLICT(id) DO UPDATE SET status=$status,body=$body";
            command.Parameters.AddWithValue("$id", run.Id);
            command.Parameters.AddWithValue("$created", run.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$status", run.Status);
            command.Parameters.AddWithValue("$body", JsonSerializer.Serialize(run, JsonDefaults.Options));
            command.ExecuteNonQuery();
        }
    }
    public Investigation? Get(string id)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT body FROM runs WHERE id=$id"; command.Parameters.AddWithValue("$id", id);
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Investigation>(json, JsonDefaults.Options) : null;
    }
    public List<Investigation> List(int limit = 100)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT body FROM runs ORDER BY created DESC LIMIT $limit"; command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader(); var result = new List<Investigation>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<Investigation>(reader.GetString(0), JsonDefaults.Options)!);
        return result;
    }
    public Investigation? Next()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT body FROM runs WHERE status='Queued' ORDER BY created LIMIT 1";
        return command.ExecuteScalar() is string json ? JsonSerializer.Deserialize<Investigation>(json, JsonDefaults.Options) : null;
    }
    public void Recover()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT body FROM runs WHERE status='Running'";
        using var reader = command.ExecuteReader(); var interrupted = new List<Investigation>();
        while (reader.Read()) interrupted.Add(JsonSerializer.Deserialize<Investigation>(reader.GetString(0), JsonDefaults.Options)!);
        reader.Close();
        foreach (var run in interrupted) { run.Status = "Failed"; run.Error = "Server stopped during this run. Start a new investigation to retry."; Save(run); }
    }
    public Investigation Review(string id, ReviewRequest request)
    {
        lock (gate)
        {
            var run = Get(id) ?? throw new KeyNotFoundException();
            if (run.Status != "AwaitingReview") throw new InvalidOperationException("This run is not awaiting review.");
            if (request.Decision is not ("Approved" or "Rejected")) throw new ArgumentException("Choose Approved or Rejected.");
            if (request.Notes is null || request.Notes.Length > 4000) throw new ArgumentException("Review notes must be present and at most 4,000 characters.");
            run.Review = request.Decision; run.ReviewNotes = Safety.Redact(request.Notes); run.ReviewedAt = DateTimeOffset.UtcNow;
            run.Status = request.Decision; Save(run); return run;
        }
    }
}
