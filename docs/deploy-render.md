# Deploy to Render

This Blueprint runs the UI and API as one Docker web service, generates a private application access key, and attaches a 1 GB persistent disk for SQLite. It is configured as a single instance because SQLite and the persistent disk are single-writer storage.

## Create the service

1. In Render, create a new Blueprint and connect this repository.
2. Review the `bug-resolution-agent` web service. It uses the Starter compute plan and a 1 GB persistent disk.
3. Apply the Blueprint and wait for the `/health` check to pass.
4. In the service's Environment page, reveal and copy `Agent__ApiKey`. Open the service URL and enter that key in the UI.
5. Select **Run sample investigation** to try the bundled demo.

The generated key protects API routes. Keep it private. Configure `Model__ApiKey` and `Model__Name` in the Render service environment to enable live model proposals. Configure `GITHUB_TOKEN` only if you enable the documented GitHub operations. These secrets are optional for the bundled demo.

## Hosted limits

The runner is disabled in this Blueprint. The application needs a Docker daemon to execute untrusted test code in isolated containers; this web service does not provide that host daemon. The UI reports runner status honestly, and does not claim tests passed. Use the local Docker setup in the README for the four-stage test workflow.

The Starter service is always on and currently costs $0.05/hour (about $36.50 for a 730-hour month). The 1 GB disk is $0.25/month. Bandwidth and any model-provider usage can add cost. Render's Free service plan cannot attach a persistent disk and would lose investigation history when it spins down.

Render Blueprint references:

- <https://render.com/docs/blueprint-spec>
- <https://render.com/docs/disks>
- <https://render.com/pricing>
