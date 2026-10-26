import type { MemberDto } from '../../../../api/projects';
import { Avatar } from '../../../../components/Avatar';
import { sprintsTestIds as ids } from '../../testIds';

interface InProgressNowProps {
  members: MemberDto[];
  /** The signed-in person's user id. */
  meId: string | undefined;
  /** What each user has in progress, by user id. */
  activeByUser: ReadonlyMap<string, string[]>;
}

/** Who is working on what right now: every member with how many items they have in progress. */
export function InProgressNow({ members, meId, activeByUser }: InProgressNowProps) {
  return (
    <section className="grid gap-1.5">
      <div className="text-[length:var(--text-xs)] font-semibold tracking-wide text-ink-muted uppercase">
        In progress now
      </div>
      <p className="m-0 text-[length:var(--text-xs)] text-ink-muted">
        Who&apos;s working on what right now.
      </p>
      {members.map(({ user }) => {
        const active = activeByUser.get(user.id) ?? [];
        return (
          <div
            key={user.id}
            data-testid={ids.railPerson}
            data-user-id={user.id}
            className="flex items-center gap-2 text-[length:var(--text-sm)]"
          >
            <Avatar {...user} />
            <span>
              {user.name}
              {user.id === meId ? <span className="text-ink-muted"> (you)</span> : null}
            </span>
            <span
              data-testid={ids.railPersonActive}
              title={active.join(', ')}
              className="ml-auto text-[length:var(--text-xs)] text-ink-muted"
            >
              {active.length > 0 ? `${active.length} active` : '—'}
            </span>
          </div>
        );
      })}
    </section>
  );
}
