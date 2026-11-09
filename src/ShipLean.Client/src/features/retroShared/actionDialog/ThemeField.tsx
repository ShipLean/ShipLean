import type { RetroPointDto, RetroThemeRefDto } from '@shiplean/core/api/retros';
import { useRetroThemes } from '../../retros/useRetroQueries';
import { retroSharedTestIds as ids } from '../testIds';
import { useRetroActionDialog } from './useRetroActionDialog';

/** The action item's one theme: the commonest among its points until someone chooses, then the choice. Verdicts judge it. */
export function ThemeField({
  value,
  chosen,
  points,
  onChoose,
}: {
  value: string;
  chosen: boolean;
  points: RetroPointDto[];
  onChoose: (themeId: string) => void;
}) {
  const { projectId } = useRetroActionDialog();
  const listed = useRetroThemes(projectId).data?.themes.map((option) => option.theme) ?? [];
  const options = new Map<string, RetroThemeRefDto>();
  for (const theme of listed) options.set(theme.id, theme);
  for (const point of points) if (point.theme) options.set(point.theme.id, point.theme);
  const shown = [...options.values()].filter(
    (theme) => theme.tone !== 'good' || theme.id === value,
  );
  return (
    <label className="grid gap-1">
      <span className="text-[length:var(--text-xs)] font-semibold text-ink-muted">Theme</span>
      <select
        data-testid={ids.retroActionTheme}
        value={value}
        className="field w-full"
        onChange={(e) => onChoose(e.target.value)}
      >
        <option value="">No theme</option>
        {shown.map((theme) => (
          <option key={theme.id} value={theme.id}>
            {theme.name}
          </option>
        ))}
      </select>
      <span className="text-[length:var(--text-xs)] text-ink-muted">
        {chosen ? '' : 'The commonest among its points. '}Verdicts judge this theme.
      </span>
    </label>
  );
}
