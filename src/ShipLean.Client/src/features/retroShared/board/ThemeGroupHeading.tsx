import type { RetroThemeRefDto } from '../../../api/retros';
import { ThemeChip } from '../ThemeChip';

// TODO: the finished theme heading styling replaces this plain heading.
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
