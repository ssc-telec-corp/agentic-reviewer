# demo-app

Synthetic target for the SIA reviewer eval. A notes service on .NET 10 with JWT auth
(bearer header or `session` cookie), role policies, an outbound link-preview client with a
host allowlist, an IP-restricted `/internal` endpoint behind trusted proxies, a static
frontend under a strict CSP, a Dockerfile and Kubernetes manifests.

Each SIA criterion has branches `sia/<criterion>/<case>` that change this directory; the
expected result of each is in `eval/corpus/cases/`. Nothing here is deployed.

Build: `dotnet build`
Run: `DEMOAPP_JWT_KEY=$(openssl rand -base64 32) DEMOAPP_SEED_PASSWORD=<pw> dotnet run`
Inputs: env `DEMOAPP_JWT_KEY` (base64, 32+ bytes), optional `DEMOAPP_SEED_PASSWORD`.
Output: HTTP on port 8080.
