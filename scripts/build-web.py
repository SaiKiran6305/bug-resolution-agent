from pathlib import Path
import shutil
import subprocess

root=Path(__file__).resolve().parents[1]
subprocess.run(['npm','ci'],cwd=root/'frontend',check=True)
subprocess.run(['npm','run','build'],cwd=root/'frontend',check=True)
destination=root/'src/BugResolution.Api/wwwroot'
if destination.exists():
    shutil.rmtree(destination)
shutil.copytree(root/'frontend/dist',destination)
print('Web build copied to the API static directory.')
