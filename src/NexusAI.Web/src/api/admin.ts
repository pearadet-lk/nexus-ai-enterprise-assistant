import { getAccessToken, keycloak } from '../auth/keycloak';

export type AdminStatsDto = {
  totalAuditEvents: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  totalCost: number;
  totalToolExecutions: number;
  totalConversations: number;
};

export type AuditLogDto = {
  id: string;
  userId: string | null;
  action: string | null;
  promptTokens: number;
  completionTokens: number;
  cost: number;
  createdAt: string;
};

export type ToolExecutionDto = {
  id: string;
  conversationId: string | null;
  toolName: string;
  durationMs: number;
  status: string;
  errorMessage?: string | null;
  createdAt: string;
};

export type McpToolDto = {
  serverId: string;
  serverName: string;
  name: string;
  description: string | null;
  inputSchemaJson: string | null;
};

export type McpServerHealthDto = {
  serverId: string;
  serverName: string;
  isHealthy: boolean;
  latencyMs: number;
  error: string | null;
  toolCount: number;
};

export type AdminDashboardDto = {
  stats: AdminStatsDto;
  recentAuditLogs: AuditLogDto[];
  recentToolExecutions: ToolExecutionDto[];
};

type ApiResponse<T> = {
  success: boolean;
  data: T | null;
  error?: string | null;
};

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

async function authorizedFetch(path: string, init?: RequestInit): Promise<Response> {
  if (keycloak.isTokenExpired(30)) {
    await keycloak.updateToken(30);
  }

  const token = getAccessToken();
  const headers = new Headers(init?.headers);
  headers.set('Content-Type', 'application/json');
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  return fetch(`${apiBaseUrl}${path}`, { ...init, headers });
}

async function readData<T>(response: Response): Promise<T> {
  const text = await response.text();
  if (!text) {
    throw new Error(response.ok
      ? 'Empty response from server'
      : `Request failed (${response.status})`);
  }

  let payload: ApiResponse<T>;
  try {
    payload = JSON.parse(text) as ApiResponse<T>;
  } catch {
    throw new Error(`Invalid response from server (${response.status})`);
  }

  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error ?? `Request failed (${response.status})`);
  }
  return payload.data;
}

export async function getAdminDashboard(): Promise<AdminDashboardDto> {
  const response = await authorizedFetch('/api/admin/dashboard');
  return readData<AdminDashboardDto>(response);
}

export async function getMcpTools(): Promise<McpToolDto[]> {
  const response = await authorizedFetch('/api/mcp/tools');
  return readData<McpToolDto[]>(response);
}

export async function getMcpHealth(): Promise<McpServerHealthDto[]> {
  const response = await authorizedFetch('/api/mcp/health');
  return readData<McpServerHealthDto[]>(response);
}
