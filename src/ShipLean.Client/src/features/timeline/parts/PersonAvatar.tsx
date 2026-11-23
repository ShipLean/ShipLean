import type { TimelinePersonDto } from '@shiplean/core/api/views';
import { AVATAR_COLOURS, previewInitials } from '../../settings/profile/identity/model';
import { timelineTestIds as ids } from '../testIds';

const pixels = { sm: 18, md: 20 } as const;
const fontSizes = { sm: 8, md: 9 } as const;

/** The same person always gets the same identity colour: a hash of their user id, or their name when they have none. */
function identityColour(person: TimelinePersonDto): string {
  const seed = person.userId ?? person.name;
  let hash = 0;
  for (const char of seed) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return AVATAR_COLOURS[hash % AVATAR_COLOURS.length];
}

/** A person's initials on their colour, "AI" for an agent, or a dashed empty circle when nobody is assigned. */
export function PersonAvatar({
  person,
  size = 'md',
}: {
  person: TimelinePersonDto | null;
  size?: 'sm' | 'md';
}) {
  const variant = person === null ? 'none' : person.agent ? 'agent' : 'person';
  const label =
    person === null ? 'Unassigned' : person.agent ? `Agent: ${person.name}` : person.name;
  const look =
    person === null
      ? { background: 'transparent', border: '1px dashed var(--color-border)' }
      : person.agent
        ? {
            background: 'var(--color-surface-2)',
            color: 'var(--color-text)',
            border: '1px solid var(--color-border)',
          }
        : { background: identityColour(person) };
  return (
    <span
      data-testid={ids.personAvatar}
      data-variant={variant}
      role="img"
      aria-label={label}
      title={label}
      className="inline-grid flex-none place-items-center rounded-full leading-none font-semibold text-signal-ink"
      style={{ width: pixels[size], height: pixels[size], fontSize: fontSizes[size], ...look }}
    >
      {person === null ? null : person.agent ? 'AI' : previewInitials(person.name)}
    </span>
  );
}
