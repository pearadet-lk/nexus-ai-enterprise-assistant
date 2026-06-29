# NexusAI on Minikube

Deploy the full NexusAI stack to a local Kubernetes cluster using [Minikube](https://minikube.sigs.k8s.io/).

## Prerequisites

- Minikube 1.32+
- kubectl
- Docker (Minikube docker driver recommended on Windows)
- 8 GB RAM allocated to Minikube (`minikube start --memory=8192 --cpus=4`)
- OpenAI API key (for chat)

Keep `config/nexusai-realm.json` in sync with `infra/keycloak/nexusai-realm.json` when updating Keycloak settings.

## Deploy (single command)

Builds all images (including frontend), applies manifests, waits for pods, and starts port-forwards in the background.

**Windows (PowerShell):**

```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"
.\minikube-deploy.ps1
```

```bash
export OPENAI_API_KEY=sk-your-key-here
./minikube-deploy.sh
```

Open **http://localhost:5173** (`demo` / `demo`).

## Teardown (single command)

Stops port-forwards, removes cluster resources, and stops Minikube:

```powershell
.\minikube-teardown.ps1
```

```bash
./minikube-teardown.sh
```

## Access URLs

| Service | URL |
|---------|-----|
| Web UI | http://localhost:5173 |
| API Gateway | http://localhost:5000 |
| Keycloak | http://localhost:8080 |
| Jaeger UI | http://localhost:16686 |
| RabbitMQ UI | http://localhost:15672 (`guest` / `guest`) |

Port-forwards run in the background (PIDs in `.minikube/port-forward.pids`).

## Set OpenAI key after deploy

```bash
kubectl -n nexusai create secret generic nexusai-secrets \
  --from-literal=mssql-sa-password='Your_strong_password123' \
  --from-literal=openai-api-key='sk-your-key' \
  --dry-run=client -o yaml | kubectl apply -f -

kubectl -n nexusai rollout restart deployment/agent-service
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Minikube cluster (namespace: nexusai)                      │
│                                                             │
│  Infra: sqlserver, redis, rabbitmq, jaeger, keycloak        │
│  Apps:  web, api-gateway, identity, context-service,        │
│         agent-service, mcp-gateway, mcp-sql, mcp-files,     │
│         mcp-jira, audit-service                             │
└─────────────────────────────────────────────────────────────┘
          ▲
          │ auto port-forward (deploy script → background)
          │
   localhost:5173 / :5000 / :8080 / :16686 / :15672
```

## Troubleshooting

- **Cannot reach localhost** — re-run deploy or `port-forward-minikube -Background` / `--background`
- **SQL Server not ready** — first image pull can take 8+ minutes; deploy script waits up to 15 minutes. `kubectl logs deploy/sqlserver -n nexusai`
- **Image pull / ErrImagePull** — app images must be built inside Minikube Docker (`minikube docker-env` then `.\scripts\deploy-minikube.ps1`). Do not run `kubectl apply -k infra/minikube` alone. Images use `imagePullPolicy: Never` so Kubernetes will not try Docker Hub.
- **Port already in use** — stop Docker full stack or local `dotnet run` / `npm run dev` on 5000/5173/8080 before deploy
- **Chat errors** — verify OpenAI secret; `kubectl logs deploy/agent-service -n nexusai`
