# Deploy to Render

This Blueprint runs the UI and API as one free Docker web service and generates a private application access key. It is intended as a no-cost preview of the bundled demo.

## Create the service

1. In Render, create a new Blueprint and connect this repository.
2. Review the `bug-resolution-agent` web service. It uses Render's Free compute plan; it creates no paid disk or database.
3. Apply the Blueprint and wait for the `/health` check to pass.
4. In the service's Environment page, reveal and copy `Agent__ApiKey`. Open the service URL and enter that key in the UI.
5. Select **Run sample investigation** to try the bundled demo.

The generated key protects API routes. Keep it private. Configure `Model__ApiKey` and `Model__Name` in the Render service environment to enable live model proposals. Configure `GITHUB_TOKEN` only if you enable the documented GitHub operations. These secrets are optional for the bundled demo.

## Hosted limits

The runner is disabled in this Blueprint. The application needs a Docker daemon to execute untrusted test code in isolated containers; this web service does not provide that host daemon. The UI reports runner status honestly, and does not claim tests passed. Use the local Docker setup in the README for the four-stage test workflow.

Render's Free service plan is $0 for hosting within its included usage. It spins down after 15 minutes without traffic and can take about a minute to start again. It has no persistent disk, so investigation history is lost when the service restarts, redeploys, or spins down. Leave model credentials unset to keep this demo from making paid model API calls; the bundled demo needs no LLM key.

Render Blueprint references:

- <https://render.com/docs/blueprint-spec>
- <https://render.com/docs/disks>
- <https://render.com/docs/free>
