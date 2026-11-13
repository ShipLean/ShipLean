import type { UserRef } from '@shiplean/core/api/refs';
import { Avatar } from '../components/Avatar';
import { realtimeTestIds as ids } from '../features/realtime/testIds';
import { Sheet } from '../touch/Sheet';

/** Everyone else on this screen, in a bottom sheet (K5): what the touch presence count opens. */
export function PresenceSheet({
  open,
  users,
  onClose,
}: {
  open: boolean;
  users: readonly UserRef[];
  onClose: () => void;
}) {
  return (
    <Sheet open={open} title="On this screen" onClose={onClose}>
      {users.map((user) => (
        <div
          key={user.id}
          data-testid={ids.presenceSheetItem}
          className="flex min-h-(--touch-target) items-center gap-2 text-[length:var(--text-base)] text-ink"
        >
          <Avatar {...user} />
          <span className="min-w-0 truncate">{user.name}</span>
        </div>
      ))}
    </Sheet>
  );
}
