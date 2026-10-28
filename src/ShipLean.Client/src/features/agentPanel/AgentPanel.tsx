import type { ProjectDto } from '../../api/projects';
import { SessionBody } from './SessionBody';
import { SessionHeader } from './SessionHeader';
import { useAgentPanel } from './agentPanelApi';
import { agentPanelTestIds as ids } from './testIds';

const button =
  'h-[var(--control-height)] rounded-control border border-edge px-3 text-[length:var(--text-sm)] hover:bg-signal-soft focus-visible:outline-2 focus-visible:outline-signal disabled:opacity-60';

// The agent on this item: the current session (header, links, plan, activity log), Stop while it is live, Start agent when
// none is, and earlier sessions folded away. Everything comes from one view model; it stays out of the way (renders nothing)
// for an item with no session in a project with no agent connection, and while loading or if the load fails.
export function AgentPanel({ project, itemKey }: { project: ProjectDto; itemKey: string }) {
  const { query, start, stop } = useAgentPanel(project.id, itemKey);
  const panel = query.data;
  if (!panel?.visible) return null;
  const [current, ...earlier] = panel.sessions;
  const failure = start.error ?? stop.error;
  return (
    <section
      data-testid={ids.panel}
      aria-label="Agent"
      className="grid max-w-[780px] gap-3 rounded-control border border-edge bg-panel p-3"
    >
      <header className="flex items-center gap-2">
        <h2 className="text-[length:var(--text-md)] font-semibold">Agent</h2>
        <span className="flex-1" />
        {current?.live ? (
          current.stopRequested ? (
            <span
              data-testid={ids.stopping}
              className="text-[length:var(--text-xs)] text-ink-muted"
            >
              Stopping…
            </span>
          ) : (
            <button
              type="button"
              data-testid={ids.stop}
              className={button}
              disabled={stop.isPending}
              onClick={() => stop.mutate()}
            >
              Stop
            </button>
          )
        ) : null}
        {panel.availability.canStart ? (
          <button
            type="button"
            data-testid={ids.start}
            className={button}
            disabled={start.isPending}
            onClick={() => start.mutate()}
          >
            Start agent
          </button>
        ) : null}
      </header>
      {failure ? (
        <p
          data-testid={ids.actionError}
          role="alert"
          className="m-0 text-[length:var(--text-sm)] text-alert"
        >
          {failure.message}
        </p>
      ) : null}
      {!panel.availability.canStart && panel.availability.reason !== 'liveSession' ? (
        <p
          data-testid={ids.startBlocked}
          className="m-0 text-[length:var(--text-sm)] text-ink-muted"
        >
          {panel.availability.message}
        </p>
      ) : null}
      {current ? (
        <div className="grid gap-3">
          <SessionHeader session={current} />
          <SessionBody session={current} />
        </div>
      ) : null}
      {earlier.length > 0 ? (
        <div data-testid={ids.earlier} className="grid gap-1 border-t border-edge/60 pt-2">
          <h3 className="m-0 text-[length:var(--text-xs)] font-semibold text-ink-muted">
            Earlier sessions
          </h3>
          {earlier.map((session) => (
            <details key={session.id} data-testid={ids.earlierSession} className="grid gap-2">
              <summary className="cursor-pointer">
                <span className="inline-flex align-middle">
                  <SessionHeader session={session} />
                </span>
              </summary>
              <div className="pt-2">
                <SessionBody session={session} />
              </div>
            </details>
          ))}
        </div>
      ) : null}
    </section>
  );
}
