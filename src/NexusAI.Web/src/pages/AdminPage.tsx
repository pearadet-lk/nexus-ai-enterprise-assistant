import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  getAdminDashboard,
  getMcpHealth,
  getMcpTools,
  type AdminDashboardDto,
  type McpServerHealthDto,
  type McpToolDto,
} from '../api/admin';
import { useAuth } from '../auth/AuthProvider';

export function AdminPage() {
  const { username, logout, isAdmin } = useAuth();
  const [dashboard, setDashboard] = useState<AdminDashboardDto | null>(null);
  const [tools, setTools] = useState<McpToolDto[]>([]);
  const [health, setHealth] = useState<McpServerHealthDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isAdmin) {
      setLoading(false);
      return;
    }

    void Promise.all([getAdminDashboard(), getMcpTools(), getMcpHealth()])
      .then(([dashboardData, toolData, healthData]) => {
        setDashboard(dashboardData);
        setTools(toolData);
        setHealth(healthData);
      })
      .catch((err: Error) => setError(err.message))
      .finally(() => setLoading(false));
  }, [isAdmin]);

  if (!isAdmin) {
    return (
      <div className="admin-page">
        <div className="admin-card">
          <h1>Admin access required</h1>
          <p>Sign in with an account that has the <code>admin</code> role.</p>
          <Link to="/">Back to chat</Link>
        </div>
      </div>
    );
  }

  return (
    <div className="admin-page">
      <header className="admin-header">
        <div>
          <h1>NexusAI Admin</h1>
          <p>Token usage, audit trail, and MCP registry</p>
        </div>
        <div className="admin-header-actions">
          <Link to="/">Chat</Link>
          <span>{username}</span>
          <button type="button" className="text-button" onClick={logout}>Sign out</button>
        </div>
      </header>

      {error && <div className="error-banner">{error}</div>}
      {loading && <p className="muted">Loading dashboard…</p>}

      {dashboard && (
        <>
          <section className="admin-grid">
            <article className="admin-card">
              <h3>Total cost</h3>
              <p className="admin-metric">${dashboard.stats.totalCost.toFixed(4)}</p>
            </article>
            <article className="admin-card">
              <h3>Prompt tokens</h3>
              <p className="admin-metric">{dashboard.stats.totalPromptTokens.toLocaleString()}</p>
            </article>
            <article className="admin-card">
              <h3>Completion tokens</h3>
              <p className="admin-metric">{dashboard.stats.totalCompletionTokens.toLocaleString()}</p>
            </article>
            <article className="admin-card">
              <h3>Tool executions</h3>
              <p className="admin-metric">{dashboard.stats.totalToolExecutions}</p>
            </article>
            <article className="admin-card">
              <h3>Conversations</h3>
              <p className="admin-metric">{dashboard.stats.totalConversations}</p>
            </article>
            <article className="admin-card">
              <h3>Audit events</h3>
              <p className="admin-metric">{dashboard.stats.totalAuditEvents}</p>
            </article>
          </section>

          <section className="admin-panels">
            <article className="admin-panel">
              <h2>MCP server health</h2>
              <ul>
                {health.map((server) => (
                  <li key={server.serverId} className={server.isHealthy ? 'healthy' : 'unhealthy'}>
                    <strong>{server.serverName}</strong>
                    <span>{server.isHealthy ? `${server.latencyMs}ms · ${server.toolCount} tools` : server.error}</span>
                  </li>
                ))}
              </ul>
            </article>

            <article className="admin-panel">
              <h2>MCP tools ({tools.length})</h2>
              <ul>
                {tools.map((tool) => (
                  <li key={`${tool.serverId}-${tool.name}`}>
                    <strong>{tool.name}</strong>
                    <span>{tool.serverName}</span>
                    <em>{tool.description}</em>
                  </li>
                ))}
              </ul>
            </article>
          </section>

          <section className="admin-panels">
            <article className="admin-panel">
              <h2>Recent audit logs</h2>
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Action</th>
                    <th>Tokens</th>
                    <th>Cost</th>
                    <th>User</th>
                  </tr>
                </thead>
                <tbody>
                  {dashboard.recentAuditLogs.map((log) => (
                    <tr key={log.id}>
                      <td>{log.action}</td>
                      <td>{log.promptTokens}+{log.completionTokens}</td>
                      <td>${log.cost.toFixed(6)}</td>
                      <td>{log.userId ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </article>

            <article className="admin-panel">
              <h2>Recent tool executions</h2>
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>Tool</th>
                    <th>Status</th>
                    <th>Duration</th>
                  </tr>
                </thead>
                <tbody>
                  {dashboard.recentToolExecutions.map((tool) => (
                    <tr key={tool.id}>
                      <td>{tool.toolName}</td>
                      <td>{tool.status}</td>
                      <td>{tool.durationMs}ms</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </article>
          </section>
        </>
      )}
    </div>
  );
}
