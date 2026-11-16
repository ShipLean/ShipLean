import type { MemberDto } from '@shiplean/core/api/projects';
import type { WorkItemDto } from '@shiplean/core/api/workItems';
import { Avatar } from '../../../../components/Avatar';
import { Sheet } from '../../../../touch/Sheet';
import { ItemLink } from '../../../itemPeek/ItemLink';
import { sprintsTestIds as ids } from '../../testIds';

interface InProgressSheetProps {
  open: boolean;
  members: MemberDto[];
  /** The signed-in person's user id. */
  meId: string | undefined;
  /** What each user has in progress, by user id. */
  activeByUser: ReadonlyMap<string, WorkItemDto[]>;
  onClose: () => void;
}

/** Every member with their active items under their name, in a bottom sheet. Tapping an item closes the sheet and opens it. */
export function InProgressSheet({
  open,
  members,
  meId,
  activeByUser,
  onClose,
}: InProgressSheetProps) {
  return (
    <Sheet
      open={open}
      title="In progress now"
      subtitle="Who is working on what right now."
      onClose={onClose}
    >
      {members.map(({ user }) => {
        const active = activeByUser.get(user.id) ?? [];
        return (
          <section
            key={user.id}
            data-testid={ids.glancePerson}
            data-user-id={user.id}
            className="-mx-3 grid gap-0.5 border-b border-edge px-4 py-2 last:border-b-0"
          >
            <div className="flex min-h-[var(--touch-target)] items-center gap-2 text-[length:var(--text-base)]">
              <Avatar {...user} size="lg" />
              <span>
                {user.name}
                {user.id === meId ? <span className="text-ink-muted"> (you)</span> : null}
              </span>
              <span
                data-testid={active.length > 0 ? undefined : ids.glanceIdle}
                className="ml-auto text-[length:var(--text-sm)] text-ink-muted"
              >
                {active.length > 0 ? `${active.length} active` : 'Nothing active'}
              </span>
            </div>
            {active.map((item) => (
              <ItemLink
                key={item.id}
                itemKey={item.key}
                testId={ids.glancePersonItem}
                onClick={onClose}
                className="flex min-h-[var(--touch-target)] items-center gap-2 rounded-control py-1 pr-1 pl-10 text-[length:var(--text-base)] text-inherit no-underline active:bg-signal-soft"
              >
                <span className="min-w-0">{item.title}</span>
                <span
                  data-testid={ids.glanceItemKey}
                  className="flex-none font-mono text-[length:var(--text-xs)] text-ink-muted"
                >
                  {item.key}
                </span>
              </ItemLink>
            ))}
          </section>
        );
      })}
    </Sheet>
  );
}
