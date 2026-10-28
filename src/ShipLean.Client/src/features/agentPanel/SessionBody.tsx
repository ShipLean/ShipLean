import type { AgentActivityDto, AgentPlanStatus, AgentSessionDto } from '../../api/agentSessions';
import { agentPanelTestIds as ids } from './testIds';

const planGlyph: Record<AgentPlanStatus, { glyph: string; label: string }> = {
  pending: { glyph: '○', label: 'Pending' },
  inProgress: { glyph: '◐', label: 'In progress' },
  completed: { glyph: '✓', label: 'Completed' },
  canceled: { glyph: '✕', label: 'Canceled' },
};

function Activity({ activity, live }: { activity: AgentActivityDto; live: boolean }) {
  const when = (
    <span className="ml-2 text-[length:var(--text-2xs)] text-ink-muted">
      {activity.createdLabel}
    </span>
  );
  const base =
    'grid gap-0.5 border-t border-edge/60 py-1.5 first:border-t-0 text-[length:var(--text-sm)]';
  if (activity.type === 'elicitation') {
    return (
      <li
        data-testid={ids.activity}
        data-type="elicitation"
        className={`${base} rounded-control border-l-2 border-l-caution bg-sunken px-2`}
      >
        <div data-testid={ids.elicitation}>
          <b className="font-semibold">The agent asks:</b> {activity.body}
          {when}
        </div>
        {live ? (
          <div
            data-testid={ids.elicitationHint}
            className="text-[length:var(--text-xs)] text-ink-muted"
          >
            Reply in a comment below to answer.
          </div>
        ) : null}
      </li>
    );
  }
  const grey = activity.ephemeral ? 'text-ink-muted opacity-70' : '';
  let content;
  switch (activity.type) {
    case 'action':
      content = (
        <>
          <b className="font-semibold">{activity.action ?? activity.body ?? 'Action'}</b>
          {activity.parameter ? <> {activity.parameter}</> : null}
          {activity.result ? <span className="text-ink-muted"> → {activity.result}</span> : null}
        </>
      );
      break;
    case 'thought':
      content = <i>{activity.body}</i>;
      break;
    case 'error':
      content = (
        <span className="text-alert">
          <b className="font-semibold">Error:</b> {activity.body}
        </span>
      );
      break;
    case 'prompt':
      content =
        activity.signal === 'stop' ? (
          <b className="font-semibold">Stop requested</b>
        ) : (
          <>
            <b className="font-semibold">Comment sent to the agent:</b> {activity.body}
          </>
        );
      break;
    default:
      content = activity.body;
  }
  return (
    <li
      data-testid={activity.ephemeral ? ids.activityEphemeral : ids.activity}
      data-type={activity.type}
      className={`${base} ${grey}`}
    >
      <div>
        {content}
        {when}
      </div>
    </li>
  );
}

// A session's links, plan and activity log. Text from the agent is rendered as text.
export function SessionBody({ session }: { session: AgentSessionDto }) {
  return (
    <div className="grid gap-3">
      {session.urls.length > 0 ? (
        <ul
          data-testid={ids.links}
          className="m-0 flex list-none flex-wrap gap-x-3 gap-y-1 p-0 text-[length:var(--text-sm)]"
        >
          {session.urls.map((link) => (
            <li key={link.url}>
              <a
                data-testid={ids.link}
                href={link.url}
                target="_blank"
                rel="noreferrer noopener"
                className="text-signal underline"
              >
                {link.label}
              </a>
            </li>
          ))}
        </ul>
      ) : null}
      {session.plan.length > 0 ? (
        <ol
          data-testid={ids.plan}
          aria-label="Plan"
          className="m-0 grid list-none gap-0.5 p-0 text-[length:var(--text-sm)]"
        >
          {session.plan.map((step, index) => {
            const { glyph, label } = planGlyph[step.status] ?? planGlyph.pending;
            return (
              <li
                key={index}
                data-testid={ids.planStep}
                data-status={step.status}
                className={`flex gap-2 ${step.status === 'canceled' ? 'text-ink-muted line-through' : ''}`}
              >
                <span aria-hidden="true" className="w-3 text-center">
                  {glyph}
                </span>
                <span className="sr-only">{label}: </span>
                <span>{step.content}</span>
              </li>
            );
          })}
        </ol>
      ) : null}
      {session.activities.length > 0 ? (
        <ol data-testid={ids.activities} aria-label="Activity" className="m-0 grid list-none p-0">
          {session.activities.map((activity) => (
            <Activity key={activity.id} activity={activity} live={session.live} />
          ))}
        </ol>
      ) : (
        <p data-testid={ids.empty} className="m-0 text-[length:var(--text-sm)] text-ink-muted">
          No activity yet.
        </p>
      )}
    </div>
  );
}
