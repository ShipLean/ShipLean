import type { RetroThemeRefDto } from '../../../api/retros';
import { ThemeChip } from '../ThemeChip';

/** A theme's heading over its notes in a column: the name (Unthemed last, as the server sends it) and a Recurring chip. */
export function ThemeGroupHeading({
  theme,
  recurring,
}: {
  theme: RetroThemeRefDto | null;
  recurring: boolean;
}) {
  return (
    <div className="px-0.5 pt-1">
      <ThemeChip theme={theme} recurring={recurring} />
    </div>
  );
}
