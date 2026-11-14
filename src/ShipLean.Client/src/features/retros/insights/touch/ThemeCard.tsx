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
import { HeatStrip } from './HeatStrip';

/**
 * One theme as a card (D1): its name and status, the heat strip with Total and "n no sprint", its action items and its last
 * change's verdict. A tap anywhere that isn't a link or a button opens the theme sheet.
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
      <div className="grid grid-cols-[minmax(0,1fr)_52px] items-center">
        <HeatStrip counts={row.counts} columns={columns} max={max} label={theme.name} />
        <div className="grid justify-items-end leading-tight">
          <b data-testid={ids.retroHeatTotal} className="text-[length:var(--text-md)] tabular-nums">
            {row.total}
          </b>
          {mode === 'sprint' ? (
            <small
              data-testid={ids.retroHeatNoSprint}
              data-count={row.noSprint}
              className="text-[length:var(--text-2xs)] whitespace-nowrap text-ink-muted"
            >
              {row.noSprint} no sprint
            </small>
          ) : null}
        </div>
      </div>
      <footer className="flex flex-wrap items-center gap-1.5">
        <div className={`flex flex-wrap items-center gap-1.5 ${ACTION_CHIPS_AT_TOUCH}`}>
          {row.actionItems.map((action) => (
            <ActionChip key={action.workItemId} action={action} />
          ))}
          <ThemeActionButton themeId={theme.id} themeName={theme.name} />
        </div>
        <span className="flex-1" />
        <VerdictChip verdict={row.lastVerdict} mode={mode} />
      </footer>
    </article>
  );
}
