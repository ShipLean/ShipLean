import { Avatar } from '../../components/Avatar';
import { Chip } from '../../components/Chip';
import type { RetroAuthorDto } from '../../api/retros';
import { retroSharedTestIds as ids } from './testIds';

/** Who wrote a point or retro: the person's avatar and first name, plus the agent's name in a purple chip when an agent wrote it. */
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
      {agentName ? <Chip tone="purple">{agentName}</Chip> : null}
    </span>
  );
}
