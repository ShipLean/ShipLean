import type {
  RetroInsightColumnDto,
  RetroInsightsMode,
  RetroThemeInsightDto,
} from '@shiplean/core/api/retros';
import { ActionChip } from '../../../retroShared/ActionChip';
import { retrosTestIds as ids } from '../../testIds';
import { StatusChip } from '../recurring/StatusChip';
import { ThemeActionButton } from '../recurring/ThemeActionButton';
import { VerdictChip } from '../recurring/VerdictChip';
import { ACTION_CHIPS_AT_TOUCH } from './actionChips';
import { StripAndTotal } from './StripAndTotal';

/**
 * One theme as a card (D1): its name and status, the heat strip with Total and "n no sprint", its action items (or
 * + Action item when it has none) and "Last change" with its verdict when one shipped. A tap anywhere that isn't a link or
 * a button opens the theme sheet.
 */
export function ThemeCard({
  row,
  columns,
  max,
  mode,
  onOpen,
}: {
  row: RetroThemeInsightDto;
  columns: readonly RetroInsightColumnDto[];
  max: number;
  mode: RetroInsightsMode;
  onOpen: (themeId: string) => void;
}) {
  const { theme } = row;
  return (
    <article
      data-testid={ids.retroThemeCard}
      data-theme-id={theme.id}
      data-status={row.status}
      className="grid cursor-pointer gap-2 rounded-panel border border-edge bg-panel px-3 py-2.5 active:border-signal"
      onClick={(event) => {
        if (!(event.target as Element).closest('a, button')) onOpen(theme.id);
      }}
    >
      <header className="flex items-start gap-2">
        <button
          type="button"
          data-testid={ids.retroThemeCardOpen}
          aria-label={`Open ${theme.name}`}
          className="min-h-[var(--touch-target)] min-w-0 flex-1 cursor-pointer text-left text-[length:var(--text-base)] leading-snug font-semibold text-ink"
          onClick={() => onOpen(theme.id)}
        >
          {theme.name}
        </button>
        <StatusChip status={row.status} mode={mode} />
      </header>
      <StripAndTotal
        counts={row.counts}
        columns={columns}
        max={max}
        label={theme.name}
        total={row.total}
        noSprint={row.noSprint}
        mode={mode}
        totalTestId={ids.retroHeatTotal}
        noSprintTestId={ids.retroHeatNoSprint}
      />
      <footer className="grid gap-1.5">
        {row.actionItems.length > 0 ? (
          <div className={`flex flex-wrap items-center gap-1.5 ${ACTION_CHIPS_AT_TOUCH}`}>
            {row.actionItems.map((action) => (
              <ActionChip key={action.workItemId} action={action} />
            ))}
          </div>
        ) : (
          <div className="flex">
            <ThemeActionButton themeId={theme.id} themeName={theme.name} />
          </div>
        )}
        {row.lastVerdict !== null ? (
          <div
            data-testid={ids.retroThemeCardLastChange}
            className="flex items-center gap-2 text-[length:var(--text-sm)] text-ink-muted"
          >
            Last change
            <VerdictChip verdict={row.lastVerdict} mode={mode} />
          </div>
        ) : null}
      </footer>
    </article>
  );
}
