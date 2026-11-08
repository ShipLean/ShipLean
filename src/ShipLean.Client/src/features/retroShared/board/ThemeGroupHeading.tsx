import type { RetroThemeRefDto } from '../../../api/retros';
import { Chip } from '../../../components/Chip';
import { retroSharedTestIds as ids } from '../testIds';
import { ThemeChip } from '../ThemeChip';

/**
 * A theme's heading over its notes in a column: the name (Unthemed last, as the server sends it) and a Recurring chip.
 * Given `onRecurring` (the touch page passes it), the chip is a 32px button that calls it to open the Recurring sheet.
 */
export function ThemeGroupHeading({
  theme,
  recurring,
  onRecurring,
}: {
  theme: RetroThemeRefDto | null;
  recurring: boolean;
  onRecurring?: () => void;
}) {
  if (!recurring || !onRecurring) {
    return (
      <div className="px-0.5 pt-1 touch:col-span-full">
        <ThemeChip theme={theme} recurring={recurring} />
      </div>
    );
  }
  return (
    <div className="flex items-center justify-between gap-1.5 px-0.5 pt-1 touch:col-span-full">
      <ThemeChip theme={theme} />
      <button
        type="button"
        data-testid={ids.retroThemeRecurring}
        aria-label={`Recurring: ${theme?.name ?? 'Unthemed'}`}
        className="inline-grid min-h-[var(--touch-target)] shrink-0 cursor-pointer place-items-center border-0 bg-transparent p-0"
        onClick={onRecurring}
      >
        <Chip tone="warn">Recurring</Chip>
      </button>
    </div>
  );
}
