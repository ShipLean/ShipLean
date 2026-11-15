import { useRetroActionDialog } from '../../../retroShared/actionDialog/useRetroActionDialog';
import { retrosTestIds as ids } from '../../testIds';

/** + Action item on a theme row: opens the action dialog on the theme's undecided points. */
export function ThemeActionButton({ themeId, themeName }: { themeId: string; themeName: string }) {
  const dialog = useRetroActionDialog();
  return (
    <button
      type="button"
      data-testid={ids.retroInsightsThemeAction}
      aria-label={`Add an action item for ${themeName}`}
      className="h-5 cursor-pointer whitespace-nowrap rounded-xs border border-dashed border-edge bg-transparent px-1.5 text-[length:var(--text-2xs)] text-ink-muted hover:border-solid hover:text-ink touch:min-h-[var(--touch-target)] touch:px-3 touch:text-[length:var(--text-sm)]"
      onClick={() => dialog.open({ themeId, scope: { kind: 'project' } })}
    >
      + Action item
    </button>
  );
}
