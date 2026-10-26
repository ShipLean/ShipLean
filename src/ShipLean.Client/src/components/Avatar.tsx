import { componentTestIds } from './testIds';

/** The shape of a persisted `UserRef` (`src/api/refs.ts`), so `<Avatar {...user} />` works. */
export interface AvatarProps {
  name?: string;
  /** Null or empty means no person: a dashed outline (for example an unassigned card). */
  initials?: string | null;
  /** Lowercase #rrggbb identity colour; it stays the same in both themes. */
  avatarColour?: string | null;
  /** Shown over the colour; the colour stays behind it while it loads. */
  photoUrl?: string | null;
  size?: 'md' | 'lg';
}

const pixels = { md: 20, lg: 22 } as const;

export function Avatar({ name, initials, avatarColour, photoUrl, size = 'md' }: AvatarProps) {
  const person = Boolean(initials);
  const px = pixels[size];
  return (
    <span
      data-testid={componentTestIds.avatar}
      data-variant={person ? 'person' : 'none'}
      title={name}
      className="relative inline-grid flex-none place-items-center overflow-hidden rounded-full text-[9px] leading-none font-semibold text-signal-ink"
      style={{
        width: px,
        height: px,
        ...(person
          ? { background: avatarColour ?? 'var(--color-muted)' }
          : { background: 'transparent', border: '1px dashed var(--color-border)' }),
      }}
    >
      {person ? initials : null}
      {person && photoUrl ? (
        <img
          data-testid={componentTestIds.avatarPhoto}
          src={photoUrl}
          alt=""
          className="absolute inset-0 size-full object-cover"
        />
      ) : null}
    </span>
  );
}
