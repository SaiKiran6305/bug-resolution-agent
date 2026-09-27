"""Create local configuration without printing or committing the generated key."""
from pathlib import Path
import secrets

root = Path(__file__).resolve().parents[1]
target = root / '.env'
if target.exists():
    print('.env already exists; left unchanged.')
else:
    text = (root / '.env.example').read_text().replace('AGENT_API_KEY=\n', f'AGENT_API_KEY={secrets.token_urlsafe(32)}\n')
    target.write_text(text)
    target.chmod(0o600)
    print('Created .env with a random application access key. Read that file locally to sign in.')
