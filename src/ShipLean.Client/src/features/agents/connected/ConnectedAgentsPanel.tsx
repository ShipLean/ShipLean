import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getConnectedAgents,
  getMcpUrl,
  revokeConnectedAgent,
  type ConnectedAgentDto,
} from '../../../api/agents';
import { Chip } from '../../../components/Chip';
import { showToast } from '../../../components/toastStore';
import { agentScopes } from '../authorize/scopes';
import { agentsTestIds as ids } from '../testIds';

export const connectedAgentsKey = ['agents', 'connected'] as const;
export const mcpUrlKey = ['agents', 'mcp-url'] as const;

// "Connected agents" on Your profile: each agent signed in as you, with Revoke, and the line that connects another.
export function ConnectedAgentsPanel() {
  const queryClient = useQueryClient();
  const agents = useQuery({ queryKey: connectedAgentsKey, queryFn: () => getConnectedAgents() });
  const mcp = useQuery({ queryKey: mcpUrlKey, queryFn: () => getMcpUrl() });
  const revoke = useMutation({
    mutationFn: (agent: ConnectedAgentDto) => revokeConnectedAgent(agent.id),
    onSuccess: (_result, agent) => showToast(`${agent.clientName} is disconnected.`),
    onError: () => showToast("Couldn't revoke that agent. Try again.", { tone: 'danger' }),
    onSettled: () => queryClient.invalidateQueries({ queryKey: connectedAgentsKey }),
  });

  return (
    <section data-testid={ids.slotConnectedAgents} className="panel mt-3 max-w-[560px] bg-panel">
      <header className="flex min-h-11 flex-wrap items-center gap-2 border-b border-edge px-3 py-2">
        <h2 className="m-0 text-[length:var(--text-md)] font-semibold">Connected agents</h2>
        <span className="text-[length:var(--text-xs)] text-ink-muted">
          Tools signed in as you. Revoking one cuts it off at once.
        </span>
      </header>
      <div data-testid={ids.connectedPanel}>
        {agents.isPending ? null : agents.isError ? (
          <p
            data-testid={ids.connectedFailed}
            className="m-0 p-3 text-[length:var(--text-sm)] text-ink-muted"
          >
            Couldn&apos;t load your connected agents. Reload the page to try again.
          </p>
        ) : agents.data.length === 0 ? (
          <p
            data-testid={ids.connectedEmpty}
            className="m-0 p-3 text-[length:var(--text-sm)] text-ink-muted"
          >
            No agents are connected.
          </p>
        ) : (
          <ul className="m-0 list-none p-0">
            {agents.data.map((agent) => (
              <li
                key={agent.id}
                data-testid={ids.connectedRow}
                className="flex items-center gap-3 border-b border-edge px-3 py-2.5 last:border-b-0"
              >
                <div className="grid min-w-0 flex-1 gap-0.5 text-[length:var(--text-sm)]">
                  <span className="flex flex-wrap items-center gap-1.5">
                    <b data-testid={ids.connectedName}>{agent.clientName}</b>
                    <span data-testid={ids.connectedHost}>
                      {agent.verified && agent.clientHost !== null ? (
                        <Chip tone="ok">{agent.clientHost}</Chip>
                      ) : (
                        <Chip tone="warn">unverified</Chip>
                      )}
                    </span>
                  </span>
                  <span data-testid={ids.connectedScopes} className="text-ink-muted">
                    {agentScopes
                      .filter((s) => agent.scopes.includes(s.scope))
                      .map((s) => s.label)
                      .join(' · ')}
                  </span>
                  <span className="text-[length:var(--text-xs)] text-ink-muted">
                    <span data-testid={ids.connectedLastUsed}>
                      {agent.lastUsedLabel === null
                        ? 'Never used'
                        : `Last used ${agent.lastUsedLabel}`}
                    </span>
                    {' · '}
                    <span data-testid={ids.connectedConnectedAt}>
                      Connected {agent.connectedLabel}
                    </span>
                  </span>
                </div>
                <button
                  type="button"
                  data-testid={ids.connectedRevoke}
                  className="btn-ghost"
                  disabled={revoke.isPending}
                  aria-label={`Revoke ${agent.clientName}`}
                  onClick={() => revoke.mutate(agent)}
                >
                  Revoke
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
      {mcp.data ? (
        <footer className="grid gap-1 rounded-b-panel border-t border-edge bg-canvas px-3 py-2 text-[length:var(--text-xs)] text-ink-muted">
          <span>To connect Claude Code, run:</span>
          <code
            data-testid={ids.connectedAddLine}
            className="block overflow-x-auto rounded-control border border-edge bg-panel px-2 py-1 whitespace-nowrap text-ink"
          >
            claude mcp add --transport http shiplean {mcp.data.mcpUrl}
          </code>
        </footer>
      ) : null}
    </section>
  );
}
