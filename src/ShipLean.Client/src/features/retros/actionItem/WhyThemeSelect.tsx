import type { RetroThemeRefDto } from '@shiplean/core/api/retros';
import { useRetroThemes } from '../useRetroQueries';
import { retrosTestIds as ids } from '../testIds';

/** The action item's theme: the project's themes that are not good ones, the current theme whatever its tone, and No theme. */
export function WhyThemeSelect({
  projectId,
  theme,
  disabled,
  onChange,
}: {
  projectId: string;
  theme: RetroThemeRefDto | null;
  disabled: boolean;
  onChange: (themeId: string | null) => void;
}) {
  const listed = useRetroThemes(projectId).data?.themes.map((option) => option.theme) ?? [];
  const options = new Map<string, RetroThemeRefDto>();
  for (const t of listed) options.set(t.id, t);
  if (theme) options.set(theme.id, theme);
  const shown = [...options.values()].filter((t) => t.tone !== 'good' || t.id === theme?.id);
  return (
    <label className="flex items-center gap-2 text-[length:var(--text-sm)]">
      <span className="text-ink-muted">Theme</span>
      <select
        data-testid={ids.retroActionWhyTheme}
        disabled={disabled}
        value={theme?.id ?? ''}
        className="field w-56"
        onChange={(e) => onChange(e.target.value === '' ? null : e.target.value)}
      >
        <option value="">No theme</option>
        {shown.map((t) => (
          <option key={t.id} value={t.id}>
            {t.name}
          </option>
        ))}
      </select>
    </label>
  );
}
