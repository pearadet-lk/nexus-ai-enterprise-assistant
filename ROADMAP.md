# NexusAI Roadmap

## Phase 1 — Foundation ✅

**Goal:** Authenticated chat shell with persistent conversations.

| Component | Status | Deliverables |
|-----------|--------|--------------|
| React Web | Done | Keycloak login, chat UI, conversation list |
| API Gateway | Done | YARP routing, JWT validation, CORS |
| Identity Service | Done | `/api/profile/me` from JWT claims |
| Context Service | Done | CRUD for conversations and messages |
| SQL Server | Done | `Conversations`, `Messages`, `ToolExecutions`, `AuditLogs` tables |
| Keycloak | Done | Realm import, demo user, OIDC client |
| Docker Compose | Done | SQL Server + Keycloak |

---

## Phase 2 — AI Layer ✅

**Goal:** Replace echo responses with real LLM answers and tool-calling foundation.

| Component | Status | Deliverables |
|-----------|--------|--------------|
| Agent Service | Done | Semantic Kernel + OpenAI, port 5003 |
| Chat API | Done | `POST /api/chat` with SSE streaming |
| Tool logging | Done | `ToolExecutions` table via Context API |
| Audit logging | Done | Token usage + cost in `AuditLogs` |
| Frontend | Done | Streaming messages, tool timeline, usage banner |
| Gateway | Done | `/api/chat` route to Agent Service |

---

## Phase 3 — MCP Integration ✅ (current)

**Goal:** Dynamic tool discovery and execution through MCP servers.

| Component | Status | Deliverables |
|-----------|--------|--------------|
| MCP Gateway | Done | Tool discovery, health, execute — port 5004 |
| SQL MCP Server | Done | `get_delayed_shipments`, `execute_read_only_query` — port 5010 |
| File MCP Server | Done | `read_document`, `search_documents` — port 5011 |
| Shipments data | Done | Seeded `Shipments` table in SQL Server |
| Agent Service | Done | MCP tools registered dynamically in Semantic Kernel |
| Admin API | Done | `GET /api/mcp/tools`, `GET /api/mcp/health`, `POST /api/mcp/refresh` |
| Gateway | Done | MCP admin routes proxied to MCP Gateway |

**Exit criteria:** User asks "show delayed shipments", agent discovers SQL MCP tool, executes query, summarizes results.

---

## Phase 4 — Agentic Orchestration ✅ (current)

**Goal:** Multi-agent pipeline with planning, memory, and review.

| Component | Status | Deliverables |
|-----------|--------|--------------|
| Planner Agent | Done | JSON execution plan from user request |
| Memory Agent | Done | Conversation summary + preferences (`ConversationMemories`) |
| Tool Agent | Done | Executes plan tool steps via MCP Gateway |
| Review Agent | Done | Validates answer against tool evidence, 1 retry |
| Agent Pipeline | Done | Memory → Plan → Tools → Synthesis → Review |
| Frontend | Done | Agent timeline UI with plan/phases/steps/review |
| SSE events | Done | `agent`, `plan`, `step`, `review`, `content_reset` |

**Exit criteria:** Complex multi-step request completes with visible plan, tool steps, and reviewed answer.

---

## Phase 5 — Enterprise Hardening ✅

**Goal:** Production-ready observability, caching, messaging, and deployment.

### New projects
- `NexusAI.AuditService`
- `NexusAI.NotificationService`

### Delivered

1. **Redis** — conversation memory cache, MCP tool metadata cache
2. **RabbitMQ** — audit events (`nexusai.audit`), notifications (`nexusai.notifications`)
3. **OpenTelemetry** — OTLP export to Jaeger across gateway and services
4. **Admin Dashboard** — `/admin` UI, `GET /api/admin/*` APIs, Keycloak `admin` role
5. **Docker** — Redis, RabbitMQ, Jaeger in `docker-compose.yml`; full stack in `docker-compose.full.yml`
6. **Azure stubs** — `infra/azure/container-apps.bicep` starter template

### Exit criteria
Full stack in Docker Compose with tracing in Jaeger, audit via RabbitMQ, admin dashboard live.

---

## Phase dependency graph

```
Phase 1 (Foundation)
    ↓
Phase 2 (AI + SK)
    ↓
Phase 3 (MCP Gateway + Servers)
    ↓
Phase 4 (Multi-Agent)
    ↓
Phase 5 (Redis, RabbitMQ, OTel, Azure)
```
