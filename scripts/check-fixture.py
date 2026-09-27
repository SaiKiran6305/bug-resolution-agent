"""Execute only our deterministic, trusted fixture on the host; never use for model code."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import tempfile

root=Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='agent-fixture-') as temporary:
    work=Path(temporary)/'checkout'
    shutil.copytree(root/'samples/checkout',work,ignore=shutil.ignore_patterns('bin','obj'))
    project=work/'tests/Checkout.Tests/Checkout.Tests.csproj'
    def run(mode,expected):
        result=subprocess.run(['dotnet','run','--project',str(project)],env={**os.environ,'BUG_AGENT_TEST_MODE':mode},capture_output=True,text=True)
        summaries=[json.loads(line[17:]) for line in result.stdout.splitlines() if line.startswith('BUG_AGENT_RESULT:')]
        assert result.returncode==expected and summaries,(result.returncode,result.stdout,result.stderr)
        assert summaries[-1]['failed']==(1 if expected else 0),summaries
        print(result.stdout.strip())
    run('suite',0)
    subprocess.run(['dotnet','run','--project',str(root/'tests/BugResolution.Tests'),'--','--export-demo-test',str(work/'tests/Checkout.Tests/AgentRegression.cs')],check=True,capture_output=True)
    run('regression',1)
    source=work/'src/Checkout.Api/Program.cs'
    source.write_text(source.read_text().replace('// BUG: a successful payment leaves the stored order Pending.\n    return Results.Ok(order);','orders[order.Id] = order with { Status = "Paid" };\n    return Results.Ok(orders[order.Id]);'))
    run('regression',0)
    run('suite',0)
print('PASS: trusted fixture exhibits failure before and success after the intended fix. This is not a Docker isolation test.')
