"""Run the host API with .env configuration; build the web UI first."""
import os
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
env = os.environ.copy()
if (root / '.env').exists():
    for line in (root / '.env').read_text().splitlines():
        if line.strip() and not line.lstrip().startswith('#') and '=' in line:
            name, value = line.split('=', 1)
            env.setdefault(name.strip(), value.strip())
mapping = {'AGENT_API_KEY':'Agent__ApiKey','MODEL_API_KEY':'Model__ApiKey','MODEL_NAME':'Model__Name','GITHUB_TOKEN':'GitHub__Token','RUNNER_ENABLED':'Runner__Enabled'}
for source, dest in mapping.items():
    if source in env:
        env[dest] = env[source]
env.setdefault('ASPNETCORE_ENVIRONMENT', 'Production')
raise SystemExit(subprocess.call(['dotnet', 'run', '--project', str(root/'src/BugResolution.Api'), '--', '--urls', 'http://127.0.0.1:5080'], cwd=root, env=env))
