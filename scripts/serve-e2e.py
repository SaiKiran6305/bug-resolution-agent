"""Disposable local server used only by browser tests."""
from pathlib import Path
import os
import subprocess
import tempfile

root=Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='agent-e2e-') as temporary:
    env={**os.environ,'Agent__ApiKey':'browser-test-key-not-a-real-secret','ASPNETCORE_ENVIRONMENT':'Production','DataDirectory':temporary+'/data','WorkspaceDirectory':temporary+'/workspaces','Services__0__RepositoryPath':str(root),'Runner__Enabled':'false'}
    env.pop('Model__ApiKey',None);env.pop('Model__Name',None);env.pop('GitHub__Token',None)
    raise SystemExit(subprocess.call(['dotnet','run','--no-build','--project',str(root/'src/BugResolution.Api'),'--','--urls','http://127.0.0.1:5081'],cwd=root,env=env))
