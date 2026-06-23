import { getAccessToken, keycloak } from '../auth/keycloak';

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

export type ApiResponse<T> = {
  success: boolean;
  data: T | null;
  error?: string | null;
};

export type MessageDto = {
  id: string;
  role: string;
  content: string;
  createdAt: string;
};

export type ConversationDto = {
  id: string;
  userId: string;
  title: string | null;
  createdAt: string;
  updatedAt: string | null;
  messages: MessageDto[];
};

export type UserProfileDto = {
  userId: string;
  email: string | null;
  displayName: string | null;
  roles: string[];
};

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

export async function getProfile(): Promise<UserProfileDto> {
  const response = await authorizedFetch('/api/profile/me');
  const payload = (await response.json()) as ApiResponse<UserProfileDto>;
  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error ?? 'Failed to load profile');
  }
  return payload.data;
}

export async function listConversations(): Promise<ConversationDto[]> {
  const response = await authorizedFetch('/api/conversations');
  const payload = (await response.json()) as ApiResponse<ConversationDto[]>;
  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error ?? 'Failed to load conversations');
  }
  return payload.data;
}

export async function createConversation(title?: string): Promise<ConversationDto> {
  const response = await authorizedFetch('/api/conversations', {
    method: 'POST',
    body: JSON.stringify({ title }),
  });
  const payload = (await response.json()) as ApiResponse<ConversationDto>;
  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error ?? 'Failed to create conversation');
  }
  return payload.data;
}

export async function getConversation(id: string): Promise<ConversationDto> {
  const response = await authorizedFetch(`/api/conversations/${id}`);
  const payload = (await response.json()) as ApiResponse<ConversationDto>;
  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error ?? 'Failed to load conversation');
  }
  return payload.data;
}

export type ToolStreamEvent = {
  toolName: string;
  durationMs: number;
  status: string;
  errorMessage?: string | null;
};

export type AgentPhaseEvent = {
  phase: string;
  status: string;
  message?: string | null;
};

export type AgentPlanStep = {
  order: number;
  title: string;
  description?: string | null;
  toolServerId?: string | null;
  toolName?: string | null;
};

export type AgentPlanEvent = {
  steps: AgentPlanStep[];
};

export type AgentStepEvent = {
  order: number;
  title: string;
  status: string;
  result?: string | null;
};

export type AgentReviewEvent = {
  approved: boolean;
  feedback: string;
  retried: boolean;
};

export type ChatDoneEvent = {
  messageId: string;
  conversationId: string;
  promptTokens: number;
  completionTokens: number;
  cost: number;
};

export type StreamChatHandlers = {
  onConversation?: (conversationId: string) => void;
  onAgent?: (event: AgentPhaseEvent) => void;
  onPlan?: (plan: AgentPlanEvent) => void;
  onStep?: (step: AgentStepEvent) => void;
  onContent?: (delta: string) => void;
  onContentReset?: (reason: string) => void;
  onTool?: (tool: ToolStreamEvent) => void;
  onReview?: (review: AgentReviewEvent) => void;
  onDone?: (result: ChatDoneEvent) => void;
  onError?: (message: string) => void;
};

export async function streamChat(
  message: string,
  conversationId: string | null,
  handlers: StreamChatHandlers,
  signal?: AbortSignal,
): Promise<void> {
  if (keycloak.isTokenExpired(30)) {
    await keycloak.updateToken(30);
  }

  const token = getAccessToken();
  const response = await fetch(`${apiBaseUrl}/api/chat`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ conversationId, message }),
    signal,
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Chat request failed (${response.status})`);
  }

  if (!response.body) {
    throw new Error('Streaming response body is empty.');
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });
    const parts = buffer.split('\n\n');
    buffer = parts.pop() ?? '';

    for (const part of parts) {
      if (!part.trim()) continue;

      const lines = part.split('\n');
      let eventType = 'message';
      let dataLine = '';

      for (const line of lines) {
        if (line.startsWith('event:')) {
          eventType = line.slice(6).trim();
        } else if (line.startsWith('data:')) {
          dataLine = line.slice(5).trim();
        }
      }

      if (!dataLine) continue;
      const data = JSON.parse(dataLine) as Record<string, unknown>;

      switch (eventType) {
        case 'conversation':
          handlers.onConversation?.(data.conversationId as string);
          break;
        case 'agent':
          handlers.onAgent?.(data as unknown as AgentPhaseEvent);
          break;
        case 'plan':
          handlers.onPlan?.(data as unknown as AgentPlanEvent);
          break;
        case 'step':
          handlers.onStep?.(data as unknown as AgentStepEvent);
          break;
        case 'content':
          handlers.onContent?.(data.delta as string);
          break;
        case 'content_reset':
          handlers.onContentReset?.((data.reason as string) ?? 'Revising answer');
          break;
        case 'tool':
          handlers.onTool?.(data as unknown as ToolStreamEvent);
          break;
        case 'review':
          handlers.onReview?.(data as unknown as AgentReviewEvent);
          break;
        case 'done':
          handlers.onDone?.(data as unknown as ChatDoneEvent);
          break;
        case 'error':
          handlers.onError?.((data.message as string) ?? 'Unknown error');
          break;
      }
    }
  }
}

export async function addMessage(conversationId: string, role: string, content: string): Promise<MessageDto> {
  const response = await authorizedFetch(`/api/conversations/${conversationId}/messages`, {
    method: 'POST',
    body: JSON.stringify({ role, content }),
  });
  const payload = (await response.json()) as ApiResponse<MessageDto>;
  if (!response.ok || !payload.success || !payload.data) {
    throw new Error(payload.error ?? 'Failed to send message');
  }
  return payload.data;
}
