import { Chip } from '../../components/Chip';
import type { RetroThemeRefDto } from '../../api/retros';
import { retroSharedTestIds as ids } from './testIds';

/** A point's theme name, "Unthemed" when it has none, and a Recurring chip when asked. */
export function ThemeChip({
  theme,
  recurring = false,
}: {
  theme: RetroThemeRefDto | null;
  recurring?: boolean;
}) {
  return (
    <span
      data-testid={ids.retroThemeChip}
      data-themed={theme ? 'true' : 'false'}
      className="inline-flex items-center gap-1.5 text-[length:var(--text-xs)] font-semibold whitespace-nowrap text-ink-muted"
    >
      {theme ? theme.name : <span className="font-normal italic">Unthemed</span>}
      {recurring ? <Chip tone="warn">Recurring</Chip> : null}
    </span>
  );
}
