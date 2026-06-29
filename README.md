# NexusAI Enterprise Assistant

AI-powered enterprise platform that connects employees, business systems, databases, and documents through MCP-enabled intelligent agents.

## Summary

Built with **.NET 10** and **React 19**. Chat requires a valid **OpenAI API key**.

| Area | What's included |
|------|-----------------|
| **Auth** | Keycloak OIDC login, JWT on all API routes, `user` and `admin` roles |
| **Chat** | SSE streaming, multi-agent pipeline (Planner → Memory → Tool → Review) |
| **MCP tools** | SQL queries, delayed shipments, document read/search, Jira incidents |
| **Data** | SQL Server — conversations, messages, audit logs, tool executions, shipments |
| **Enterprise** | Redis cache, RabbitMQ audit pipeline, OpenTelemetry → Jaeger, admin dashboard |
| **Deploy** | `docker compose -f docker-compose.full.yml up -d` · `minikube-deploy.ps1` / `minikube-deploy.sh` |

Future work: see [ROADMAP.md](ROADMAP.md). System design: see [ARCHITECTURE.md](ARCHITECTURE.md).

---

## Architecture

```mermaid
flowchart LR
  Web[React Web :5173] --> GW[API Gateway :5000]
  GW --> Identity[Identity :5001]
  GW --> Context[Context Service :5002]
  GW --> Agent[Agent Service :5003]
  GW --> MCP[MCP Gateway :5004]
  Agent --> Context
  Agent --> MCP
  MCP --> SqlMcp[SQL MCP :5010]
  MCP --> FileMcp[Files MCP :5011]
  MCP --> JiraMcp[Jira MCP :5012]
  Context --> SQL[(SQL Server)]
  Context --> Redis[(Redis)]
  Context --> RMQ[(RabbitMQ)]
  RMQ --> Audit[Audit Service]
  RMQ --> Notify[Notification Service]
  Agent --> OTel[Jaeger OTLP :4317]
```

**Request flow (chat):** Browser → API Gateway (JWT) → Agent Service → Planner/Memory/Tool/Review agents → MCP Gateway → SQL/File MCP servers → streamed response back to UI.

For a full system walkthrough (components, sequences, data flow, deployment topologies), see **[ARCHITECTURE.md](ARCHITECTURE.md)**.

---

## Prerequisites

| Tool | Version | When you need it |
|------|---------|------------------|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | **Docker full stack** (recommended quick start) |
| OpenAI API key | — | AI chat (required for chat) |
| [Minikube](https://minikube.sigs.k8s.io/) + kubectl | Optional | Kubernetes deploy (8 GB RAM recommended) |
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.x | Local dev only (`dotnet run`) |
| [Node.js](https://nodejs.org/) | 20+ | Local dev only (`npm run dev`) |

**App login:** `demo` / `demo` (chat) · `admin` / `admin` (chat + admin dashboard)

---

## Deploy overview

| | [Docker deploy](#docker-deploy) | [Minikube deploy](#minikube-deploy) |
|---|--------------------------------|-------------------------------------|
| **What runs in cluster/containers** | Everything (backend + frontend) | Everything (backend + frontend) |
| **Frontend** | `web` container | `web` pod |
| **Browser access** | Direct after `up -d` | Auto port-forward after deploy script |
| **Deploy command** | `docker compose -f docker-compose.full.yml up -d` | `minikube-deploy.ps1` / `minikube-deploy.sh` |
| **Teardown command** | `docker compose -f docker-compose.full.yml down` | `minikube-teardown.ps1` / `minikube-teardown.sh` |
| **Run from** | Repository root | Repository root |
| **Prerequisites** | Docker Desktop | Minikube + kubectl + Docker |

---

## Docker deploy

All services run in Docker — infrastructure, backend, and React frontend (nginx). No extra terminals needed after deploy.

**File:** `docker-compose.full.yml` (includes `docker-compose.yml` for SQL, Keycloak, Redis, RabbitMQ, Jaeger)

### Deploy commands

Run from the **repository root**. First build can take several minutes.

**Windows PowerShell — full deploy:**

```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"
docker compose -f docker-compose.full.yml build
docker compose -f docker-compose.full.yml up -d
docker compose -f docker-compose.full.yml ps
```

**Linux / macOS — full deploy:**

```bash
export OPENAI_API_KEY=sk-your-key-here
docker compose -f docker-compose.full.yml build
docker compose -f docker-compose.full.yml up -d
docker compose -f docker-compose.full.yml ps
```

Wait **30–90 seconds** for SQL Server and Keycloak, then open **http://localhost:5173**.

**Other Docker commands:**

```bash
# Health check
curl http://localhost:5000/api/health

# Follow logs
docker compose -f docker-compose.full.yml logs -f web

# Rebuild one service after code change
docker compose -f docker-compose.full.yml build agent-service
docker compose -f docker-compose.full.yml up -d agent-service

# Fix missing OpenAI key
docker compose -f docker-compose.full.yml up -d agent-service   # after setting OPENAI_API_KEY

# Stop
docker compose -f docker-compose.full.yml down

# Stop and delete data (destructive)
docker compose -f docker-compose.full.yml down -v
```

### Docker URLs

| What | URL | Login |
|------|-----|-------|
| **Web UI** (start here) | http://localhost:5173 | `demo` / `demo` or `admin` / `admin` |
| API health | http://localhost:5000/api/health | — |
| Keycloak | http://localhost:8080 | app users above |
| Keycloak admin | http://localhost:8080/admin | `admin` / `admin` |
| Jaeger (traces) | http://localhost:16686 | — |
| RabbitMQ UI | http://localhost:15672 | `guest` / `guest` |
| SQL Server | `localhost:1433` | `sa` / `Your_strong_password123` |
| Redis | `localhost:6380` | — |

Backend services (identity, agent, MCP servers, workers) have **no host port** — the UI reaches them via the API Gateway on `:5000`.

---

## Minikube deploy

Runs **everything in the cluster** — backend services and React frontend (`web` pod).

Use the **root wrapper scripts** (they call `scripts/deploy-minikube.*` and `scripts/teardown-minikube.*` for you):

| Root command (run from repo root) | Calls | Purpose |
|-----------------------------------|-------|---------|
| `minikube-deploy.ps1` / `minikube-deploy.sh` | `scripts/deploy-minikube.*` | Start Minikube if needed → build → deploy → **auto port-forward** |
| `minikube-teardown.ps1` / `minikube-teardown.sh` | `scripts/teardown-minikube.*` | **Stop port-forwards** → delete cluster → stop Minikube |

Kubernetes manifests: `infra/minikube/`

### Deploy (single command)

From the **repository root**:

**Windows PowerShell:**

```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"
.\minikube-deploy.ps1
```

**Linux / macOS:**

```bash
export OPENAI_API_KEY=sk-your-key-here
chmod +x minikube-deploy.sh minikube-teardown.sh
./minikube-deploy.sh
```

What happens:

1. **Starts Minikube** if it is not already running (8 GB RAM / 4 CPUs)
2. Builds all images inside Minikube’s Docker (including `web`)
3. Applies manifests and waits for pods
4. Starts port-forwards in the background → `.minikube/port-forward.pids`

When the script finishes, open **http://localhost:5173** (`demo` / `demo`).

```bash
kubectl -n nexusai get pods -w    # optional — watch progress
curl http://localhost:5000/api/health
```

### Teardown (single command)

From the **repository root** — stops port-forwards, removes NexusAI from the cluster, stops Minikube:

**Windows PowerShell:**

```powershell
.\minikube-teardown.ps1
```

**Linux / macOS:**

```bash
./minikube-teardown.sh
```

Keep the Minikube VM for a faster next deploy:

```powershell
.\minikube-teardown.ps1 -KeepMinikube    # Windows
./minikube-teardown.sh --keep-minikube   # Linux / macOS
```

### Minikube URLs

| What | URL | Login |
|------|-----|-------|
| **Web UI** (start here) | http://localhost:5173 | `demo` / `demo` or `admin` / `admin` |
| API health | http://localhost:5000/api/health | — |
| Keycloak | http://localhost:8080 | app users above |
| Keycloak admin | http://localhost:8080/admin | `admin` / `admin` |
| Jaeger (traces) | http://localhost:16686 | — |
| RabbitMQ UI | http://localhost:15672 | `guest` / `guest` |

Port-forwards start automatically when you run `minikube-deploy`. Re-run deploy if URLs stop responding.

### Advanced (`scripts/`)

Lower-level scripts (usually not needed if you use the root wrappers):

```bash
# Set OpenAI key after deploy
kubectl -n nexusai create secret generic nexusai-secrets \
  --from-literal=mssql-sa-password='Your_strong_password123' \
  --from-literal=openai-api-key='sk-your-key' \
  --dry-run=client -o yaml | kubectl apply -f -
kubectl -n nexusai rollout restart deployment/agent-service

# Logs
kubectl -n nexusai logs -f deployment/web

# Port-forward only (stop / debug) — deploy already starts these in background
.\scripts\port-forward-minikube.ps1 -Stop          # Windows
./scripts/port-forward-minikube.sh --stop          # Linux / macOS
```

More detail: [infra/minikube/README.md](infra/minikube/README.md)

---

## Local development

For day-to-day coding: Docker for infrastructure only, services via `dotnet run`, frontend via `npm run dev`.

### Clone and build

```bash
git clone <your-repo-url>
cd nexus-ai-enterprise-assistant

dotnet build NexusAI.sln
cd src/NexusAI.Web && npm install && npm run build && cd ../..
```

### Start infrastructure

From the repository root:

```bash
docker compose up -d
```

Wait **30–90 seconds** for SQL Server and Keycloak to become healthy.

```bash
docker compose ps
```

| Service | URL | Credentials |
|---------|-----|-------------|
| Keycloak | http://localhost:8080 | `admin` / `admin` |
| SQL Server | `localhost:1433` | `sa` / `Your_strong_password123` |
| Redis | `localhost:6380` | — |
| RabbitMQ UI | http://localhost:15672 | `guest` / `guest` |
| Jaeger | http://localhost:16686 | — |

If Keycloak was running before a realm file change: `docker compose restart keycloak`

### OpenAI key

Chat will not work without an API key. Use **user secrets** (recommended):

```bash
dotnet user-secrets init --project src/NexusAI.AgentService
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-key-here" --project src/NexusAI.AgentService
```

**Windows PowerShell alternative:**

```powershell
$env:OpenAI__ApiKey = "sk-your-key-here"   # session only
```

Default model: `gpt-4o-mini` (in `src/NexusAI.AgentService/appsettings.json`).

### Start backend services (one terminal each)

Each service needs its **own terminal**. Start in this order — MCP servers before the gateway that calls them:

| Order | Command | Port |
|-------|---------|------|
| 1 | `dotnet run --project src/NexusAI.McpServers.Sql` | 5010 |
| 2 | `dotnet run --project src/NexusAI.McpServers.Files` | 5011 |
| 3 | `dotnet run --project src/NexusAI.McpServers.Jira` | 5012 |
| 4 | `dotnet run --project src/NexusAI.McpGateway` | 5004 |
| 5 | `dotnet run --project src/NexusAI.Identity` | 5001 |
| 6 | `dotnet run --project src/NexusAI.ContextService` | 5002 |
| 7 | `dotnet run --project src/NexusAI.AgentService` | 5003 |
| 8 | `dotnet run --project src/NexusAI.ApiGateway` | 5000 |
| 9 | `dotnet run --project src/NexusAI.AuditService` | worker |
| 10 | `dotnet run --project src/NexusAI.NotificationService` | worker |

Steps 9–10 are **workers** (no HTTP port). They process RabbitMQ audit/notification queues. Chat works without them, but audit events will not flow through the async pipeline until they are running.

**Wait for** Context Service to finish migrations before relying on chat (watch its console for startup completion).

### Start frontend

```bash
cd src/NexusAI.Web
npm install
npm run dev
```

Open **http://localhost:5173** — same URLs as [Docker deploy](#docker-urls) and [Minikube deploy](#minikube-urls).

### Verify

**Gateway health (no auth):**

```bash
curl http://localhost:5000/api/health
```

**Sign in** at http://localhost:5173, create a conversation, and try a sample prompt:

- *"Which shipments from Thailand are delayed more than 3 days?"* → uses SQL MCP + `get_delayed_shipments`
- *"Search documents for shipping delay policy"* → uses File MCP
- *"What is our escalation process for delayed Thailand shipments?"* → multi-agent + document tools
- *"Create a Jira incident for delayed Thailand shipments per policy"* → SQL + files + Jira MCP (`create_incident`)

**Admin dashboard** (as `admin`): http://localhost:5173/admin — token usage, audit logs, MCP server health.

**Jaeger:** http://localhost:16686 — traces from all services exporting OTLP to port `4317`.

**RabbitMQ:** after a chat session, check the Audit Service console for `Audit consumer listening on nexusai.audit`.

---

## Configuration reference

### Connection strings (Context Service)

Defined in `src/NexusAI.ContextService/appsettings.json`:

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:NexusDb` | `Server=localhost,1433;...` | SQL Server |
| `ConnectionStrings:Redis` | `localhost:6380` | Distributed cache |
| `ConnectionStrings:RabbitMq` | `amqp://guest:guest@localhost:5672` | Audit publish |

### OpenAI (Agent Service)

| Key | Default | Purpose |
|-----|---------|---------|
| `OpenAI:ApiKey` | *(empty)* | **Required** for chat |
| `OpenAI:Model` | `gpt-4o-mini` | LLM model |
| `OpenAI:InputCostPerMillion` | `0.15` | Cost tracking |
| `OpenAI:OutputCostPerMillion` | `0.60` | Cost tracking |

### Frontend (Vite)

| Variable | Default | Purpose |
|----------|---------|---------|
| `VITE_API_BASE_URL` | `http://localhost:5000` | API Gateway |
| `VITE_KEYCLOAK_URL` | `http://localhost:8080` | Keycloak base URL |
| `VITE_KEYCLOAK_REALM` | `nexusai` | Realm name |
| `VITE_KEYCLOAK_CLIENT_ID` | `nexusai-web` | Public OIDC client |

### Keycloak

- Realm file: `infra/keycloak/nexusai-realm.json`
- JWT `roles` claim maps to ASP.NET `RoleClaimType = "roles"`
- `admin` role required for `/api/admin/*` and `/admin` UI

---

## MCP tools available to the agent

| Tool | Server | Description |
|------|--------|-------------|
| `get_delayed_shipments` | SQL MCP | Lists shipments with delay thresholds |
| `execute_read_only_query` | SQL MCP | Read-only SQL against NexusAI DB |
| `read_document` | File MCP | Read files under `data/documents/` |
| `search_documents` | File MCP | Full-text search across sandbox docs |

Sample documents:

- `data/documents/policies/shipping-delay.md`
- `data/documents/runbooks/thailand-logistics.md`

Refresh MCP tool registry (after server changes):

```bash
curl -X POST http://localhost:5000/api/mcp/refresh -H "Authorization: Bearer <token>"
```

---

## Messaging pipeline

```
Context Service  ──publish──▶  nexusai.audit  ──consume──▶  Audit Service
                                                                    │
                                         cost ≥ $0.01 ──publish──▶  nexusai.notifications
                                                                              │
                                                                    Notification Service (logs alert)
```

Audit rows are always saved to SQL. RabbitMQ carries async processing and high-cost alerts.

---

## API routes (via gateway)

All routes except `/api/health` require a valid Keycloak JWT (`Authorization: Bearer <token>`).

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/health` | — | Gateway health |
| GET | `/api/profile/me` | user | Current user profile |
| GET | `/api/conversations` | user | List conversations |
| POST | `/api/conversations` | user | Create conversation |
| GET | `/api/conversations/{id}` | user | Conversation + messages |
| GET | `/api/conversations/{id}/memory` | user | Conversation memory |
| PUT | `/api/conversations/{id}/memory` | user | Update memory |
| POST | `/api/conversations/{id}/messages` | user | Add message |
| POST | `/api/chat` | user | Stream AI response (SSE) |
| GET | `/api/mcp/tools` | user | MCP tool catalog |
| GET | `/api/mcp/health` | user | MCP server health |
| POST | `/api/mcp/refresh` | user | Re-discover tools |
| GET | `/api/admin/dashboard` | **admin** | Stats + recent logs |
| GET | `/api/admin/stats` | **admin** | Token usage aggregates |
| GET | `/api/admin/audit-logs` | **admin** | Audit log list |
| GET | `/api/admin/tool-executions` | **admin** | Tool execution list |
| POST | `/api/tool-executions` | user | Log tool run (internal) |
| POST | `/api/audit-logs` | user | Log token usage (internal) |

### Chat SSE event types

`conversation`, `agent`, `plan`, `step`, `tool`, `content`, `content_reset`, `review`, `done`, `error`

---

## Solution structure

```
src/
├── NexusAI.ApiGateway/           # YARP reverse proxy + JWT
├── NexusAI.AgentService/         # Semantic Kernel + multi-agent pipeline
├── NexusAI.McpGateway/           # MCP orchestrator + Redis tool cache
├── NexusAI.McpServers.Sql/       # SQL MCP (HTTP)
├── NexusAI.McpServers.Files/     # File MCP (HTTP)
├── NexusAI.McpServers.Jira/      # Jira MCP (HTTP, mock store)
├── NexusAI.Identity/             # User profile API
├── NexusAI.ContextService/       # Conversations, memory, audit, admin
├── NexusAI.AuditService/         # RabbitMQ audit consumer (worker)
├── NexusAI.NotificationService/  # RabbitMQ notification consumer (worker)
├── NexusAI.Contracts/            # Shared DTOs
├── NexusAI.SharedKernel/         # Redis, RabbitMQ, OpenTelemetry
└── NexusAI.Web/                  # React + Vite frontend

infra/
├── keycloak/nexusai-realm.json   # Realm import (users, roles, clients)
├── docker/                       # Per-service Dockerfiles (+ nginx.web.conf)
│   ├── Dockerfile.web            # React build → nginx
│   └── Dockerfile.*              # .NET services
├── minikube/                     # Kubernetes manifests
└── azure/                        # Container Apps Bicep stub

data/documents/                   # Sandbox files for File MCP server
data/jira/                        # Mock Jira issues (Jira MCP)

# Deploy (repository root)
docker-compose.yml                # Infrastructure only (local dev)
docker-compose.full.yml           # Docker full stack
minikube-deploy.ps1 / .sh         # Minikube: deploy (+ start Minikube, port-forward)
minikube-teardown.ps1 / .sh       # Minikube: teardown (+ stop port-forward)

scripts/                          # Called by minikube-deploy / minikube-teardown
```

---

## Troubleshooting

| Problem | Likely cause | Fix |
|---------|--------------|-----|
| Redirect loop / login fails | Keycloak not ready or wrong `VITE_KEYCLOAK_URL` | Wait for Keycloak; check `.env` (local dev) or rebuild `web` image (Docker) |
| `demo` / `admin` login rejected | Realm not imported | `docker compose restart keycloak` (or `-f docker-compose.full.yml`) |
| Chat returns error immediately | Missing OpenAI key | Local: user secret or `OpenAI__ApiKey`; Docker: set `OPENAI_API_KEY` before `up`, then `docker compose -f docker-compose.full.yml up -d agent-service` |
| 401 on API calls | Token expired or gateway not running | Refresh page; ensure API Gateway on :5000 |
| 403 on `/admin` | User lacks `admin` role | Sign in as `admin` / `admin` |
| MCP tools not found | MCP servers not started | Local: start SQL + File + Jira MCP before MCP Gateway; Docker: check `mcp-sql`, `mcp-files`, `mcp-jira` containers |
| SQL tool errors | Context Service not migrated | Check Context Service logs; verify SQL container healthy |
| Redis / RabbitMQ errors | Infra not up | `docker compose up -d`; check `docker compose ps` |
| `Bind for 0.0.0.0:6379 failed` | Another Redis on port 6379 | NexusAI uses host port **6380**; connect to `localhost:6380` or stop the other container (`docker ps`) |
| Port 5173 already in use | Vite dev + Docker `web` both running | Stop one: `docker compose -f docker-compose.full.yml stop web` or quit `npm run dev` |
| No traces in Jaeger | OTLP endpoint unreachable | Ensure Jaeger container running on port 4317 |
| Cannot reach `:5000` / `:5173` (Minikube) | Port-forwards stopped | Re-run `.\minikube-deploy.ps1` or `./minikube-deploy.sh` |

**Check logs:** each `dotnet run` terminal shows service-specific errors. For Docker: `docker compose -f docker-compose.full.yml logs -f <service>` (e.g. `web`, `agent-service`, `api-gateway`).

**Reset database (destructive):**

```bash
docker compose -f docker-compose.full.yml down -v
docker compose -f docker-compose.full.yml up -d
# Context Service will recreate schema on next start
```

---

## Build commands

```bash
dotnet build NexusAI.sln
dotnet test NexusAI.sln

cd src/NexusAI.Web
npm install
npm run build
npm run lint
```

---

## License

MIT — see [LICENSE](LICENSE).
