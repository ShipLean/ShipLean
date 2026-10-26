import { useState } from 'react';
import type { MemberDto } from '../api/projects';
import type { UserRef } from '../api/refs';
import { Avatar } from '../components/Avatar';
import { realtimeTestIds as ids } from '../features/realtime/testIds';
import { withoutMe } from './presence';

const SHOWN = 3;

/**
 * Who else has this screen open: up to three avatars, then +n; a click lists everyone. `userIds` are the ids the hub
 * listed on the screen and `members` the project's members (their `UserRef`s carry the photo); `meId` is left out, and
 * so is anyone who is not (yet) a known member.
 */
export function PresenceStack({
  userIds,
  members,
  meId,
}: {
  userIds: readonly string[];
  members: readonly MemberDto[];
  meId: string | undefined;
}) {
  const [open, setOpen] = useState(false);
  const users = withoutMe(userIds, meId).flatMap((id): UserRef[] => {
    const member = members.find((m) => m.user.id === id);
    return member === undefined ? [] : [member.user];
  });
  if (users.length === 0) return null;
  return (
    <span data-testid={ids.presenceStack} className="relative inline-flex items-center">
      <button
        type="button"
        data-testid={ids.presenceToggle}
        aria-expanded={open}
        aria-label={`${users.length} ${users.length === 1 ? 'person' : 'people'} on this screen`}
        className="inline-flex items-center rounded-full border-0 bg-transparent px-1 py-0.5 hover:bg-sunken focus-visible:outline-2 focus-visible:outline-signal"
        onClick={() => setOpen((value) => !value)}
      >
        {users.slice(0, SHOWN).map((user, index) => (
          <span
            key={user.id}
            data-testid={ids.presenceAvatar}
            className="inline-flex rounded-full"
            style={{
              marginLeft: index === 0 ? 0 : -4,
              boxShadow: '0 0 0 2px var(--color-surface)',
            }}
          >
            <Avatar {...user} size="lg" />
          </span>
        ))}
        {users.length > SHOWN ? (
          <span
            data-testid={ids.presenceMore}
            className="ml-[-4px] inline-grid h-[22px] min-w-[22px] place-items-center rounded-full bg-sunken px-1 text-[9px] font-semibold text-ink"
            style={{ boxShadow: '0 0 0 2px var(--color-surface)' }}
          >
            +{users.length - SHOWN}
          </span>
        ) : null}
      </button>
      {open ? (
        <div
          data-testid={ids.presenceList}
          className="absolute top-[calc(100%+6px)] right-0 z-50 grid min-w-[200px] gap-1.5 rounded-[var(--radius-md)] border border-edge bg-panel p-2 text-[length:var(--text-sm)] text-ink shadow-[var(--shadow-raised)]"
        >
          {users.map((user) => (
            <div
              key={user.id}
              data-testid={ids.presenceListItem}
              className="flex items-center gap-2"
            >
              <Avatar {...user} />
              <span>{user.name}</span>
            </div>
          ))}
        </div>
      ) : null}
    </span>
  );
}
