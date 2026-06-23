# NexusAI on Minikube

Deploy the full NexusAI stack to a local Kubernetes cluster using [Minikube](https://minikube.sigs.k8s.io/).

## Prerequisites

- Minikube 1.32+
- kubectl
- Docker (Minikube docker driver recommended on Windows)
- 8 GB RAM allocated to Minikube (`minikube start --memory=8192 --cpus=4`)
- OpenAI API key (for chat)

Keep `config/nexusai-realm.json` in sync with `infra/keycloak/nexusai-realm.json` when updating Keycloak settings.

## Quick deploy

### 1. Deploy the stack

**Windows (PowerShell):**

```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"   # optional but required for chat
.\scripts\deploy-minikube.ps1
```

**Linux / macOS:**

```bash
export OPENAI_API_KEY=sk-your-key-here
./scripts/deploy-minikube.sh
```

The deploy script will:

1. Start Minikube (if not running)
2. Build all service images inside Minikube's Docker daemon
3. Apply manifests from `infra/minikube/`
4. Wait for core pods to become ready

### 2. Port-forward (required)

Services use **ClusterIP** inside the cluster. Forward them to the **same localhost ports as local dev** — run this in a **separate terminal** and leave it open:

**Windows:**

```powershell
.\scripts\port-forward-minikube.ps1
```

**Linux / macOS:**

```bash
chmod +x scripts/port-forward-minikube.sh
./scripts/port-forward-minikube.sh
```

**Manual forwards** (equivalent):

```bash
kubectl port-forward -n nexusai svc/api-gateway 5000:8080 &
kubectl port-forward -n nexusai svc/keycloak    8080:8080 &
kubectl port-forward -n nexusai svc/jaeger      16686:16686 &
kubectl port-forward -n nexusai svc/rabbitmq    15672:15672 &
```

Press **Ctrl+C** in the port-forward terminal to stop all forwards.

### 3. Run the frontend

```bash
cd src/NexusAI.Web
cp .env .env.local          # Windows: copy .env .env.local
npm install
npm run dev
```

Open http://localhost:5173

| User | Password | Access |
|------|----------|--------|
| demo | demo | Chat |
| admin | admin | Chat + `/admin` |

## Access URLs (after port-forward)

| Service | URL |
|---------|-----|
| API Gateway | http://localhost:5000 |
| Keycloak | http://localhost:8080 |
| Jaeger UI | http://localhost:16686 |
| RabbitMQ UI | http://localhost:15672 (`guest` / `guest`) |

## Manual deploy steps

```bash
minikube start --memory=8192 --cpus=4
eval $(minikube docker-env)          # Linux/macOS
# minikube docker-env | Invoke-Expression   # PowerShell

docker build -t nexusai/api-gateway:local -f infra/docker/Dockerfile.gateway .
docker build -t nexusai/identity:local -f infra/docker/Dockerfile.identity .
docker build -t nexusai/context-service:local -f infra/docker/Dockerfile.context .
docker build -t nexusai/agent-service:local -f infra/docker/Dockerfile.agent .
docker build -t nexusai/mcp-gateway:local -f infra/docker/Dockerfile.mcp-gateway .
docker build -t nexusai/mcp-sql:local -f infra/docker/Dockerfile.mcp-sql .
docker build -t nexusai/mcp-files:local -f infra/docker/Dockerfile.mcp-files .
docker build -t nexusai/audit-service:local -f infra/docker/Dockerfile.audit .
docker build -t nexusai/notification-service:local -f infra/docker/Dockerfile.notification .

kubectl apply -k infra/minikube
./scripts/port-forward-minikube.sh
```

### Set OpenAI key after deploy

```bash
kubectl -n nexusai create secret generic nexusai-secrets \
  --from-literal=mssql-sa-password='Your_strong_password123' \
  --from-literal=openai-api-key='sk-your-key' \
  --dry-run=client -o yaml | kubectl apply -f -

kubectl -n nexusai rollout restart deployment/agent-service
```

## Teardown

```bash
# Stop port-forward terminal with Ctrl+C first
kubectl delete -k infra/minikube
minikube stop
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Minikube cluster (namespace: nexusai)                      │
│  ClusterIP services — not reachable from host directly    │
│                                                             │
│  Infra: sqlserver, redis, rabbitmq, jaeger, keycloak        │
│  Apps:  api-gateway, identity, context-service,             │
│         agent-service, mcp-gateway, mcp-sql, mcp-files,     │
│         audit-service, notification-service                 │
└─────────────────────────────────────────────────────────────┘
          ▲
          │ kubectl port-forward (scripts/port-forward-minikube.*)
          │
   localhost:5000 / :8080 / :16686 / :15672
          ▲
   React dev server (localhost:5173)
```

## Troubleshooting

- **Cannot reach localhost:5000** — port-forward script not running; start `port-forward-minikube` in a separate terminal
- **Port already in use** — stop local `dotnet run` or docker-compose services using 5000/8080
- **SQL Server not ready** — needs ~60s on first start; check `kubectl logs deploy/sqlserver -n nexusai`
- **Image pull errors** — images must be built with `minikube docker-env` active; they use tag `*:local` with `imagePullPolicy: IfNotPresent`
- **Keycloak login fails** — ensure port-forward is active and `VITE_KEYCLOAK_URL=http://localhost:8080` in `.env.local`
- **Chat returns errors** — verify OpenAI secret and `kubectl logs deploy/agent-service -n nexusai`
