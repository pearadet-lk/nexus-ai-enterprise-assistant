import { Link } from 'react-router-dom';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import {
  createConversation,
  getConversation,
  listConversations,
  streamChat,
  type AgentPhaseEvent,
  type AgentPlanStep,
  type AgentReviewEvent,
  type AgentStepEvent,
  type ConversationDto,
  type MessageDto,
  type ToolStreamEvent,
} from '../api/client';
import { useAuth } from '../auth/AuthProvider';

type AgentTimeline = {
  phases: AgentPhaseEvent[];
  plan: AgentPlanStep[];
  steps: AgentStepEvent[];
  review: AgentReviewEvent | null;
};

type StreamingMessage = MessageDto & {
  isStreaming?: boolean;
  tools?: ToolStreamEvent[];
  timeline?: AgentTimeline;
};

const emptyTimeline = (): AgentTimeline => ({
  phases: [],
  plan: [],
  steps: [],
  review: null,
});

export function ChatPage() {
  const { username, logout, isAdmin } = useAuth();
  const [conversations, setConversations] = useState<ConversationDto[]>([]);
  const [activeConversation, setActiveConversation] = useState<ConversationDto | null>(null);
  const [messages, setMessages] = useState<StreamingMessage[]>([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [usage, setUsage] = useState<{ promptTokens: number; completionTokens: number; cost: number } | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    void listConversations()
      .then(setConversations)
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    setMessages(activeConversation?.messages ?? []);
    setUsage(null);
  }, [activeConversation]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, sending]);

  async function handleNewConversation() {
    setError(null);
    try {
      const conversation = await createConversation();
      setConversations((current) => [conversation, ...current]);
      setActiveConversation(conversation);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create conversation');
    }
  }

  async function handleSelectConversation(id: string) {
    setError(null);
    try {
      const conversation = await getConversation(id);
      setActiveConversation(conversation);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load conversation');
    }
  }

  function updateAssistantMessage(
    assistantId: string,
    updater: (message: StreamingMessage) => StreamingMessage,
  ) {
    setMessages((current) =>
      current.map((message) => (message.id === assistantId ? updater(message) : message)),
    );
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!input.trim() || sending) return;

    const userText = input.trim();
    setSending(true);
    setError(null);
    setInput('');
    setUsage(null);

    const tempUserMessage: StreamingMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: userText,
      createdAt: new Date().toISOString(),
    };

    const tempAssistantId = crypto.randomUUID();
    const tempAssistantMessage: StreamingMessage = {
      id: tempAssistantId,
      role: 'assistant',
      content: '',
      createdAt: new Date().toISOString(),
      isStreaming: true,
      tools: [],
      timeline: emptyTimeline(),
    };

    setMessages((current) => [...current, tempUserMessage, tempAssistantMessage]);

    let conversationId = activeConversation?.id ?? null;

    try {
      await streamChat(userText, conversationId, {
        onConversation: (id) => {
          conversationId = id;
          if (!activeConversation) {
            void getConversation(id).then((conversation) => {
              setActiveConversation(conversation);
              setConversations((current) => {
                if (current.some((item) => item.id === conversation.id)) return current;
                return [conversation, ...current];
              });
            });
          }
        },
        onAgent: (agentEvent) => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            timeline: {
              ...(message.timeline ?? emptyTimeline()),
              phases: [...(message.timeline?.phases ?? []), agentEvent],
            },
          }));
        },
        onPlan: (plan) => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            timeline: {
              ...(message.timeline ?? emptyTimeline()),
              plan: plan.steps ?? [],
            },
          }));
        },
        onStep: (step) => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            timeline: {
              ...(message.timeline ?? emptyTimeline()),
              steps: [...(message.timeline?.steps ?? []).filter((item) => item.order !== step.order), step],
            },
          }));
        },
        onContent: (delta) => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            content: message.content + delta,
          }));
        },
        onContentReset: () => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            content: '',
          }));
        },
        onTool: (tool) => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            tools: [...(message.tools ?? []), tool],
          }));
        },
        onReview: (review) => {
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            timeline: {
              ...(message.timeline ?? emptyTimeline()),
              review,
            },
          }));
        },
        onDone: (result) => {
          setUsage({
            promptTokens: result.promptTokens,
            completionTokens: result.completionTokens,
            cost: result.cost,
          });
          updateAssistantMessage(tempAssistantId, (message) => ({
            ...message,
            id: result.messageId,
            isStreaming: false,
          }));
          if (conversationId) {
            void getConversation(conversationId).then(setActiveConversation);
            void listConversations().then(setConversations);
          }
        },
        onError: (message) => setError(message),
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to send message');
      setMessages((current) => current.filter((message) => message.id !== tempAssistantId));
    } finally {
      setSending(false);
      updateAssistantMessage(tempAssistantId, (message) => ({
        ...message,
        isStreaming: false,
      }));
    }
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-header">
          <h1>NexusAI</h1>
          <p>Enterprise Assistant</p>
        </div>
        <button type="button" className="primary-button" onClick={() => void handleNewConversation()}>
          New chat
        </button>
        <div className="conversation-list">
          {loading && <p className="muted">Loading conversations…</p>}
          {conversations.map((conversation) => (
            <button
              key={conversation.id}
              type="button"
              className={`conversation-item ${activeConversation?.id === conversation.id ? 'active' : ''}`}
              onClick={() => void handleSelectConversation(conversation.id)}
            >
              {conversation.title ?? 'Untitled'}
            </button>
          ))}
        </div>
        <div className="sidebar-footer">
          {isAdmin && (
            <Link className="admin-link" to="/admin">
              Admin dashboard
            </Link>
          )}
          <span>{username}</span>
          <button type="button" className="text-button" onClick={logout}>
            Sign out
          </button>
        </div>
      </aside>

      <main className="chat-panel">
        <header className="chat-header">
          <h2>{activeConversation?.title ?? 'Start a conversation'}</h2>
          <span className="badge">Phase 5 · Enterprise</span>
        </header>

        {error && <div className="error-banner">{error}</div>}

        <section className="messages">
          {messages.length ? (
            messages.map((message) => (
              <article key={message.id} className={`message ${message.role}`}>
                <div className="message-role">{message.role}</div>

                {message.timeline && (message.timeline.phases.length > 0 || message.timeline.plan.length > 0) && (
                  <details className="agent-timeline" open={message.isStreaming}>
                    <summary>Agent pipeline</summary>

                    {message.timeline.plan.length > 0 && (
                      <div className="timeline-section">
                        <h4>Plan</h4>
                        <ol>
                          {message.timeline.plan.map((step) => (
                            <li key={step.order}>
                              <strong>{step.title}</strong>
                              {step.toolName && <span className="tool-tag">{step.toolName}</span>}
                            </li>
                          ))}
                        </ol>
                      </div>
                    )}

                    {message.timeline.phases.length > 0 && (
                      <div className="timeline-section">
                        <h4>Phases</h4>
                        <ul>
                          {message.timeline.phases.map((phase, index) => (
                            <li key={`${phase.phase}-${phase.status}-${index}`} className={phase.status}>
                              <strong>{phase.phase}</strong>
                              <span>{phase.status}</span>
                              {phase.message && <em>{phase.message}</em>}
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    {message.timeline.steps.length > 0 && (
                      <div className="timeline-section">
                        <h4>Tool steps</h4>
                        <ul>
                          {message.timeline.steps.map((step) => (
                            <li key={step.order} className={step.status}>
                              <strong>{step.order}. {step.title}</strong>
                              <span>{step.status}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    )}

                    {message.timeline.review && (
                      <div className={`timeline-section review ${message.timeline.review.approved ? 'approved' : 'rejected'}`}>
                        <h4>Review</h4>
                        <p>{message.timeline.review.approved ? 'Approved' : 'Revised'} — {message.timeline.review.feedback}</p>
                      </div>
                    )}
                  </details>
                )}

                {message.tools && message.tools.length > 0 && (
                  <details className="tool-timeline" open={message.isStreaming}>
                    <summary>Tool executions ({message.tools.length})</summary>
                    <ul>
                      {message.tools.map((tool, index) => (
                        <li key={`${tool.toolName}-${index}`} className={tool.status}>
                          <strong>{tool.toolName}</strong>
                          <span>{tool.durationMs}ms · {tool.status}</span>
                          {tool.errorMessage && <em>{tool.errorMessage}</em>}
                        </li>
                      ))}
                    </ul>
                  </details>
                )}

                <p>
                  {message.content}
                  {message.isStreaming && <span className="cursor">▍</span>}
                </p>
              </article>
            ))
          ) : (
            <div className="empty-state">
              <h3>Ask NexusAI anything</h3>
              <p>Multi-agent pipeline: Plan → Tools → Review. Try: "Show delayed shipments from Thailand and summarize policy steps".</p>
            </div>
          )}
          <div ref={messagesEndRef} />
        </section>

        {usage && (
          <div className="usage-banner">
            Tokens: {usage.promptTokens} prompt · {usage.completionTokens} completion · ${usage.cost.toFixed(6)}
          </div>
        )}

        <form className="composer" onSubmit={(event) => void handleSubmit(event)}>
          <textarea
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="Show delayed shipments from Thailand and summarize the policy…"
            rows={3}
            disabled={sending}
          />
          <button type="submit" className="primary-button" disabled={sending || !input.trim()}>
            {sending ? 'Agents working…' : 'Send'}
          </button>
        </form>
      </main>
    </div>
  );
}
