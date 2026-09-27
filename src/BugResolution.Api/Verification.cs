using System.Text.Json;

namespace BugResolution.Api;

public sealed class Verification(IConfiguration config)
{
    public bool Enabled => config.GetValue<bool>("Runner:Enabled");
    public async Task<Execution> Execute(string stage, string workspace, ServiceDefinition service, string mode, CancellationToken ct)
    {
        var name = "bug-agent-" + Guid.NewGuid().ToString("N");
        Safety.RelativePath(service.TestProject);
        var arguments = new List<string> { "run", "--rm", "--name", name, "--network", "none", "--memory", "1g", "--cpus", "1", "--pids-limit", "128", "--cap-drop", "ALL", "--security-opt", "no-new-privileges", "--read-only", "--user", "65532:65532", "--tmpfs", "/work:rw,exec,nosuid,size=536870912,uid=65532,gid=65532", "--tmpfs", "/tmp:rw,exec,nosuid,size=268435456,uid=65532,gid=65532", "--mount", $"type=bind,src={Path.GetFullPath(workspace)},dst=/input,readonly", "--env", $"BUG_AGENT_TEST_MODE={mode}", service.RunnerImage, service.TestProject };
        CommandResult result;
        try { result = await Commands.Run("docker", arguments, null, Math.Clamp(config.GetValue("Runner:TimeoutSeconds", 120), 10, 300), ct); }
        catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Docker CLI is unavailable. Install Docker or disable Runner__Enabled."); }
        finally
        {
            // A killed CLI does not necessarily stop its container.
            try { await Commands.Run("docker", ["rm", "-f", name], null, 10, CancellationToken.None); } catch (System.ComponentModel.Win32Exception) { }
        }
        var protocolValid = false; var passed = 0; var failed = 0;
        foreach (var line in result.Output.Split('\n'))
        {
            if (!line.StartsWith("BUG_AGENT_RESULT:")) continue;
            try
            {
                using var doc = JsonDocument.Parse(line[17..]); var root = doc.RootElement;
                passed = root.GetProperty("passed").GetInt32(); failed = root.GetProperty("failed").GetInt32();
                protocolValid = root.GetProperty("mode").GetString() == mode && passed >= 0 && failed >= 0 && passed + failed > 0;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { protocolValid = false; }
        }
        var output = Safety.Redact(result.Output);
        if (output.Length > 24000) output = output[..12000] + "\n[output truncated]\n" + output[^12000..];
        return new(stage, $"docker: {service.RunnerImage} / {mode} / {service.TestProject}", result.ExitCode, result.TimedOut, output, result.DurationMs, protocolValid, passed, failed);
    }
    public static bool Passed(Execution execution) => !execution.TimedOut && execution.ExitCode == 0 && execution.ProtocolValid && execution.Failed == 0 && execution.Passed > 0;
    public static bool Reproduced(Execution execution) => !execution.TimedOut && execution.ExitCode == 1 && execution.ProtocolValid && execution.Failed > 0;
}
