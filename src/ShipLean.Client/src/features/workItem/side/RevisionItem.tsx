import type { RevisionDto } from '../../../api/workItems';
import { Avatar } from '../../../components/Avatar';
import { workItemTestIds as ids } from '../testIds';
import { RevisionChange } from './RevisionChange';

// One revision: who (an agent's change reads "<person> via <agent>", a webhook's names the pull or merge request), when
// (the label comes from the server), the Jira or webhook mark, and the fields it changed.
export function RevisionItem({ revision }: { revision: RevisionDto }) {
  const who = revision.changedBy?.name ?? revision.webhook ?? 'Jira import';
  return (
    <li
      data-testid={ids.revision}
      data-rev={revision.rev}
      className="grid grid-cols-[22px_minmax(0,1fr)] gap-2 pb-3 last:pb-0"
    >
      <span
        data-testid={ids.revisionNumber}
        title={`Revision ${revision.rev}`}
        className="grid size-5.5 place-items-center rounded-full border border-edge bg-surface text-[length:var(--text-2xs)] text-ink-muted"
      >
        {revision.rev}
      </span>
      <div className="grid gap-1">
        <div className="flex flex-wrap items-center gap-1.5 text-[length:var(--text-sm)]">
          <Avatar {...(revision.changedBy ?? {})} />
          <b data-testid={ids.revisionWho} className="font-medium">
            {revision.agentName ? `${who} via ${revision.agentName}` : who}
          </b>
          <span
            data-testid={ids.revisionWhen}
            className="text-[length:var(--text-xs)] text-ink-muted"
          >
            {revision.changedAtLabel}
          </span>
          {revision.origin === 'jira' ? (
            <span
              data-testid={ids.revisionFromJira}
              className="rounded-control border border-edge px-1 text-[length:var(--text-2xs)] text-ink-muted"
            >
              from Jira
            </span>
          ) : null}
          {revision.origin === 'webhook' ? (
            <span
              data-testid={ids.revisionWebhook}
              className="rounded-control border border-edge px-1 text-[length:var(--text-2xs)] text-ink-muted"
            >
              webhook
            </span>
          ) : null}
        </div>
        <ul className="m-0 grid list-none gap-0.5 p-0">
          {revision.changes.map((change) => (
            <RevisionChange key={change.field} change={change} />
          ))}
        </ul>
      </div>
    </li>
  );
}
