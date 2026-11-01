import { Avatar } from '../../components/Avatar';
import type { RetroAuthorDto } from '../../api/retros';
import { retroSharedTestIds as ids } from './testIds';

/** Who wrote a point or retro: the person's avatar and first name, plus the agent's name in a square-cornered purple tag (the design's `.agent`) when an agent wrote it. */
export function AuthorChip({ author }: { author: RetroAuthorDto }) {
  const { user, agentName, role } = author;
  return (
    <span
      data-testid={ids.retroAuthorChip}
      data-role={role}
      className="inline-flex items-center gap-1.5 text-[length:var(--text-xs)] whitespace-nowrap text-ink"
    >
      <Avatar {...user} />
      <span>{user.name.split(' ')[0]}</span>
      {agentName ? (
        <span
          data-agent=""
          className="inline-flex h-[18px] items-center rounded-[var(--radius-xs)] px-1.5 text-[length:var(--text-2xs)] font-medium whitespace-nowrap text-ink"
          style={{ background: 'color-mix(in srgb, var(--color-purple) 14%, transparent)' }}
        >
          {agentName}
        </span>
      ) : null}
    </span>
  );
}
