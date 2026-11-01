import { Chip } from '../../components/Chip';
import type { RetroThemeRefDto } from '../../api/retros';
import { retroSharedTestIds as ids } from './testIds';

/**
 * A point's theme. `chip` (default) shows the name, "Unthemed" when it has none, and a Recurring chip when asked.
 * `line` is the design's `.theme-i`: a muted line reading `# Theme name`, with no Recurring chip, and nothing when unthemed.
 */
export function ThemeChip({
  theme,
  recurring = false,
  variant = 'chip',
}: {
  theme: RetroThemeRefDto | null;
  recurring?: boolean;
  variant?: 'chip' | 'line';
}) {
  if (variant === 'line') {
    return theme ? (
      <span
        data-testid={ids.retroThemeChip}
        data-themed="true"
        data-variant="line"
        className="block text-[length:var(--text-xs)] text-ink-muted"
      >
        # {theme.name}
      </span>
    ) : null;
  }
  return (
    <span
      data-testid={ids.retroThemeChip}
      data-themed={theme ? 'true' : 'false'}
      data-variant="chip"
      className="inline-flex items-center gap-1.5 text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-ink-muted"
    >
      {theme ? theme.name : <span className="font-normal italic">Unthemed</span>}
      {recurring ? <Chip tone="warn">Recurring</Chip> : null}
    </span>
  );
}
