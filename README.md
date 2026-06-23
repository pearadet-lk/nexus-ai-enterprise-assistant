# NexusAI Enterprise Assistant

AI-powered enterprise platform that connects employees, business systems, databases, and documents through MCP-enabled intelligent agents.

**Status:** Phases 1–5 are implemented. The solution builds with .NET 10 and React 19. Chat requires a valid OpenAI API key.

---

## What this project includes

| Area | Capabilities |
|------|----------------|
| **Auth** | Keycloak OIDC login, JWT on all API routes, `user` and `admin` roles |
| **Chat** | Streaming SSE responses, multi-agent pipeline (Planner → Memory → Tool → Review) |
| **MCP tools** | SQL queries, delayed shipments, document read/search |
| **Data** | SQL Server for conversations, messages, audit logs, tool executions, shipments |
| **Enterprise** | Redis cache, RabbitMQ audit pipeline, OpenTelemetry → Jaeger, admin dashboard |

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
  Context --> SQL[(SQL Server)]
  Context --> Redis[(Redis)]
  Context --> RMQ[(RabbitMQ)]
  RMQ --> Audit[Audit Service]
  RMQ --> Notify[Notification Service]
  Agent --> OTel[Jaeger OTLP :4317]
```

**Request flow (chat):** Browser → API Gateway (JWT) → Agent Service → Planner/Memory/Tool/Review agents → MCP Gateway → SQL/File MCP servers → streamed response back to UI.

---

## Prerequisites

| Tool | Version | Required for |
|------|---------|--------------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.x | Backend services |
| [Node.js](https://nodejs.org/) | 20+ | Frontend |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest | SQL, Keycloak, Redis, RabbitMQ, Jaeger |
| OpenAI API key | — | AI chat (required for chat) |
| [Minikube](https://minikube.sigs.k8s.io/) + kubectl | Optional | Kubernetes local deploy |
| `dotnet-ef` global tool | Optional | Manual migrations only |

```bash
dotnet tool install --global dotnet-ef   # only if you run migrations manually
```

---

## Choose how to run

| Mode | Best for | Infra | App services | Frontend |
|------|----------|-------|--------------|----------|
| **A. Local dev** (recommended) | Day-to-day development | Docker Compose | `dotnet run` in terminals | `npm run dev` |
| **B. Docker full stack** | All-in-containers smoke test | Included in compose | Docker images | Run locally or add later |
| **C. Minikube** | Kubernetes testing | In cluster | In cluster | `npm run dev` + `port-forward-minikube` |

---

## A. Local development (recommended)

### Step 1 — Clone and verify build

```bash
git clone <your-repo-url>
cd nexus-ai-enterprise-assistant

dotnet build NexusAI.sln
cd src/NexusAI.Web && npm install && npm run build && cd ../..
```

### Step 2 — Start infrastructure

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
| Keycloak admin console | http://localhost:8080 | `admin` / `admin` |
| SQL Server | `localhost:1433` | `sa` / `Your_strong_password123` |
| Redis | `localhost:6379` | — |
| RabbitMQ management | http://localhost:15672 | `guest` / `guest` |
| Jaeger UI | http://localhost:16686 | — |

**Important — Keycloak realm:** The `nexusai` realm (users `demo` / `admin`) is imported on first start. If Keycloak was already running before a realm file change, restart it:

```bash
docker compose restart keycloak
```

### Step 3 — Database

Migrations run **automatically** when Context Service starts. To apply them manually:

```bash
dotnet ef database update --project src/NexusAI.ContextService
```

On first Context Service start, sample **shipment** data is seeded (`SHP-1001` … `SHP-1004`) for MCP SQL demos.

### Step 4 — Configure OpenAI

Chat will not work without an API key. Use **user secrets** (recommended):

```bash
dotnet user-secrets init --project src/NexusAI.AgentService
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-key-here" --project src/NexusAI.AgentService
```

**Windows PowerShell alternative:**

```powershell
$env:OpenAI__ApiKey = "sk-your-key-here"   # session only
```

Default model: `gpt-4o-mini` (configurable in `src/NexusAI.AgentService/appsettings.json`).

### Step 5 — Start backend services

Each service needs its **own terminal**. Start in this order — MCP servers before the gateway that calls them:

| Order | Command | Port |
|-------|---------|------|
| 1 | `dotnet run --project src/NexusAI.McpServers.Sql` | 5010 |
| 2 | `dotnet run --project src/NexusAI.McpServers.Files` | 5011 |
| 3 | `dotnet run --project src/NexusAI.McpGateway` | 5004 |
| 4 | `dotnet run --project src/NexusAI.Identity` | 5001 |
| 5 | `dotnet run --project src/NexusAI.ContextService` | 5002 |
| 6 | `dotnet run --project src/NexusAI.AgentService` | 5003 |
| 7 | `dotnet run --project src/NexusAI.ApiGateway` | 5000 |
| 8 | `dotnet run --project src/NexusAI.AuditService` | worker |
| 9 | `dotnet run --project src/NexusAI.NotificationService` | worker |

Steps 8–9 are **workers** (no HTTP port). They process RabbitMQ audit/notification queues. Chat works without them, but audit events will not flow through the async pipeline until they are running.

**Wait for** Context Service to finish migrations before relying on chat (watch its console for startup completion).

### Step 6 — Start the frontend

```bash
cd src/NexusAI.Web
npm install
npm run dev
```

Default environment is in `src/NexusAI.Web/.env`:

```env
VITE_API_BASE_URL=http://localhost:5000
VITE_KEYCLOAK_URL=http://localhost:8080
VITE_KEYCLOAK_REALM=nexusai
VITE_KEYCLOAK_CLIENT_ID=nexusai-web
```

Open **http://localhost:5173**

| App user | Password | Access |
|----------|----------|--------|
| `demo` | `demo` | Chat |
| `admin` | `admin` | Chat + [Admin dashboard](http://localhost:5173/admin) |

### Step 7 — Verify it is working

**Gateway health (no auth):**

```bash
curl http://localhost:5000/api/health
```

**Sign in** at http://localhost:5173, create a conversation, and try a sample prompt:

- *"Which shipments from Thailand are delayed more than 3 days?"* → uses SQL MCP + `get_delayed_shipments`
- *"Search documents for shipping delay policy"* → uses File MCP
- *"What is our escalation process for delayed Thailand shipments?"* → multi-agent + document tools

**Admin dashboard** (as `admin`): http://localhost:5173/admin — token usage, audit logs, MCP server health.

**Jaeger:** http://localhost:16686 — traces from all services exporting OTLP to port `4317`.

**RabbitMQ:** after a chat session, check the Audit Service console for `Audit consumer listening on nexusai.audit`.

---

## B. Full Docker stack

Builds and runs all backend services as containers (infrastructure + apps).

```bash
export OPENAI_API_KEY=sk-your-key-here          # Linux/macOS
# $env:OPENAI_API_KEY = "sk-your-key-here"      # Windows PowerShell

docker compose -f docker-compose.full.yml build
docker compose -f docker-compose.full.yml up -d
```

| Service | URL |
|---------|-----|
| API Gateway | http://localhost:5000 |
| Keycloak | http://localhost:8080 |

Run the frontend locally (same `.env` as local dev). Dockerfiles live in `infra/docker/`.

---

## C. Minikube (Kubernetes)

Requires Minikube with **8 GB RAM** recommended.

**1. Deploy:**

**Windows:**

```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"
.\scripts\deploy-minikube.ps1
```

**Linux / macOS:**

```bash
export OPENAI_API_KEY=sk-your-key-here
./scripts/deploy-minikube.sh
```

**2. Port-forward** (required — run in a **separate terminal**, leave open):

```powershell
.\scripts\port-forward-minikube.ps1          # Windows
./scripts/port-forward-minikube.sh         # Linux/macOS
```

Forwards cluster services to the **same ports as local dev**:

| Local port | Service |
|------------|---------|
| 5000 | API Gateway |
| 8080 | Keycloak |
| 16686 | Jaeger UI |
| 15672 | RabbitMQ UI |

**3. Frontend:**

```bash
cd src/NexusAI.Web
cp .env .env.local    # Windows: copy .env .env.local
npm run dev
```

Full details: [infra/minikube/README.md](infra/minikube/README.md)

---

## Ports reference

| Service | Port | Notes |
|---------|------|-------|
| API Gateway | 5000 | Single entry point for the UI |
| Identity | 5001 | Profile API |
| Context Service | 5002 | Conversations, memory, audit, admin |
| Agent Service | 5003 | Chat + agent pipeline |
| MCP Gateway | 5004 | Tool discovery and execution |
| SQL MCP Server | 5010 | Internal — called by MCP Gateway |
| File MCP Server | 5011 | Internal — reads `data/documents/` |
| Keycloak | 8080 | Auth |
| SQL Server | 1433 | Database |
| Redis | 6379 | Cache |
| RabbitMQ AMQP | 5672 | Messaging |
| RabbitMQ UI | 15672 | Management console |
| Jaeger UI | 16686 | Traces |
| OTLP collector | 4317 | OpenTelemetry export |
| React dev server | 5173 | Frontend |

---

## Configuration reference

### Connection strings (Context Service)

Defined in `src/NexusAI.ContextService/appsettings.json`:

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:NexusDb` | `Server=localhost,1433;...` | SQL Server |
| `ConnectionStrings:Redis` | `localhost:6379` | Distributed cache |
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

## Messaging pipeline (Phase 5)

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
├── NexusAI.Identity/             # User profile API
├── NexusAI.ContextService/       # Conversations, memory, audit, admin
├── NexusAI.AuditService/         # RabbitMQ audit consumer (worker)
├── NexusAI.NotificationService/  # RabbitMQ notification consumer (worker)
├── NexusAI.Contracts/            # Shared DTOs
├── NexusAI.SharedKernel/         # Redis, RabbitMQ, OpenTelemetry
└── NexusAI.Web/                  # React + Vite frontend

infra/
├── keycloak/nexusai-realm.json   # Realm import (users, roles, clients)
├── docker/                       # Per-service Dockerfiles
├── minikube/                     # Kubernetes manifests
└── azure/                        # Container Apps Bicep stub

data/documents/                   # Sandbox files for File MCP server
scripts/                          # deploy-minikube.*, port-forward-minikube.*
docker-compose.yml                # Infrastructure only
docker-compose.full.yml           # Infrastructure + all services
```

---

## Troubleshooting

| Problem | Likely cause | Fix |
|---------|--------------|-----|
| Redirect loop / login fails | Keycloak not ready or wrong `VITE_KEYCLOAK_URL` | Wait for Keycloak; check `.env` matches your run mode |
| `demo` / `admin` login rejected | Realm not imported | `docker compose restart keycloak` |
| Chat returns error immediately | Missing OpenAI key | Set user secret or `OpenAI__ApiKey` env var; restart Agent Service |
| 401 on API calls | Token expired or gateway not running | Refresh page; ensure API Gateway on :5000 |
| 403 on `/admin` | User lacks `admin` role | Sign in as `admin` / `admin` |
| MCP tools not found | MCP servers not started | Start SQL + File MCP servers before MCP Gateway |
| SQL tool errors | Context Service not migrated | Check Context Service logs; verify SQL container healthy |
| Redis / RabbitMQ errors | Infra not up | `docker compose up -d`; check `docker compose ps` |
| No traces in Jaeger | OTLP endpoint unreachable | Ensure Jaeger container running on port 4317 |
| Port already in use | Previous run still active | Stop old `dotnet run` processes or change `launchSettings.json` |

**Check logs:** each `dotnet run` terminal shows service-specific errors. For Docker: `docker compose logs -f <service>`.

**Reset database (destructive):**

```bash
docker compose down -v
docker compose up -d
# Context Service will recreate schema on next start
```

---

## Build commands

```bash
# Backend
dotnet build NexusAI.sln
dotnet test NexusAI.sln                    # if tests exist

# Frontend
cd src/NexusAI.Web
npm run build                              # production build → dist/
npm run lint
```

---

## Feature phases (summary)

<details>
<summary>Phase 1 — Foundation</summary>

React + Keycloak, API Gateway (YARP + JWT), Identity, Context Service, SQL Server, Docker Compose.
</details>

<details>
<summary>Phase 2 — AI Layer</summary>

Semantic Kernel + OpenAI, SSE streaming chat, tool execution logging, audit trail.
</details>

<details>
<summary>Phase 3 — MCP Integration</summary>

MCP Gateway, SQL and File MCP servers, dynamic tool loading, admin MCP APIs.
</details>

<details>
<summary>Phase 4 — Agentic Orchestration</summary>

Planner, Memory, Tool, and Review agents; agent pipeline timeline in the UI.
</details>

<details>
<summary>Phase 5 — Enterprise Hardening</summary>

Redis, RabbitMQ, OpenTelemetry/Jaeger, admin dashboard, Docker full stack, Minikube, Azure stubs.
</details>

See [ROADMAP.md](ROADMAP.md) for the full phased plan.

---

## License

MIT — see [LICENSE](LICENSE).
