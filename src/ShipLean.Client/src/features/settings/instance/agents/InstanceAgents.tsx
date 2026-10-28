import { useState } from 'react';
import { Panel } from '../../draft/Panel';
import { settingsTestIds as ids } from '../../testIds';
import { AccessState } from '../AccessState';
import { useAgentConnections, useCreateConnection } from './agentsApi';
import { ConnectionForm } from './ConnectionForm';
import { ConnectionRow } from './ConnectionRow';
import { shownOf, type AgentConnectionDto, type ShownOnce } from './model';
import { ShownOnceDialog } from './ShownOnceDialog';

/** The instance admin's Agents tab: Linear-compatible agent connections. Keys are shown once, in a dialog. */
export function InstanceAgents() {
  const connections = useAgentConnections();
  return (
    <AccessState pending={connections.isPending} error={connections.error}>
      {connections.data ? <AgentsPanel connections={connections.data} /> : null}
    </AccessState>
  );
}

function AgentsPanel({ connections }: { connections: AgentConnectionDto[] }) {
  const [adding, setAdding] = useState(false);
  // The only copy of a new secret or token the browser holds: Done drops it.
  const [shown, setShown] = useState<ShownOnce | null>(null);
  const create = useCreateConnection();
  const live = connections.filter((c) => c.archivedAt === null);
  const archived = connections.filter((c) => c.archivedAt !== null);
  return (
    <div data-testid={ids.agentsPanel} className="p-4">
      <Panel title="Agent connections">
        <p className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          A Linear-compatible agent receives a signed webhook when an item enters a state that
          starts an agent, and reports back with the callback token. A project picks one connection
          in its settings.
        </p>
        {live.length === 0 && !adding ? (
          <p
            data-testid={ids.agentsEmpty}
            className="m-0 text-[length:var(--text-sm)] text-ink-muted"
          >
            No connections yet.
          </p>
        ) : null}
        {live.map((c) => (
          <ConnectionRow key={c.id} connection={c} onShown={setShown} />
        ))}
        {adding ? (
          <ConnectionForm
            initial={{ name: '', receiverUrl: '' }}
            submitLabel="Add connection"
            pending={create.isPending}
            error={create.error}
            onCancel={() => {
              create.reset();
              setAdding(false);
            }}
            onSubmit={(body) =>
              create.mutate(body, {
                onSuccess: (issued) => {
                  setAdding(false);
                  setShown(shownOf(issued));
                },
              })
            }
          />
        ) : (
          <div>
            <button
              type="button"
              data-testid={ids.agentsAdd}
              className="btn-signal"
              onClick={() => {
                create.reset();
                setAdding(true);
              }}
            >
              Add connection
            </button>
          </div>
        )}
      </Panel>
      {archived.length > 0 ? (
        <Panel title="Archived">
          {archived.map((c) => (
            <ConnectionRow key={c.id} connection={c} onShown={setShown} />
          ))}
        </Panel>
      ) : null}
      {shown ? <ShownOnceDialog shown={shown} onDone={() => setShown(null)} /> : null}
    </div>
  );
}
