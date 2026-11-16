import { Link, useSearchParams } from 'react-router-dom';
import type {
  RetroInsightColumnDto,
  RetroInsightsMode,
  RetroUnthemedDto,
} from '@shiplean/core/api/retros';
import { routePath } from '../../../../routes';
import { retrosTestIds as ids } from '../../testIds';
import { StripAndTotal } from './StripAndTotal';
import { UNTHEMED_SHEET_ID, withThemeSheet } from './themeSheetAddress';

/**
 * Points with no theme as the last, dashed card at touch (D1): its counts, "not judged", and the link to give them themes.
 * Like a theme card, its name or a tap anywhere that isn't a link or a button opens its sheet (`sheet=theme:unthemed`),
 * which says what Unthemed means.
 */
export function UnthemedCard({
  projectKey,
  unthemed,
  columns,
  max,
  mode,
}: {
  projectKey: string;
  unthemed: RetroUnthemedDto;
  columns: readonly RetroInsightColumnDto[];
  max: number;
  mode: RetroInsightsMode;
}) {
  const [params, setParams] = useSearchParams();
  const openSheet = () => setParams(withThemeSheet(params, UNTHEMED_SHEET_ID), { replace: true });
  return (
    <article
      data-testid={ids.retroInsightsUnthemed}
      className="grid cursor-pointer gap-2 rounded-panel border border-dashed border-edge bg-canvas px-3 py-2.5 active:border-signal"
      onClick={(event) => {
        if (!(event.target as Element).closest('a, button')) openSheet();
      }}
    >
      <header className="flex items-center gap-2">
        <button
          type="button"
          data-testid={ids.retroUnthemedCardOpen}
          aria-label="Open Unthemed"
          className="min-h-[var(--touch-target)] min-w-0 flex-1 cursor-pointer text-left text-[length:var(--text-base)] font-semibold text-ink italic"
          onClick={openSheet}
        >
          Unthemed
        </button>
        <span className="text-[length:var(--text-xs)] text-ink-muted">not judged</span>
      </header>
      <StripAndTotal
        counts={unthemed.counts}
        columns={columns}
        max={max}
        label="Unthemed"
        total={unthemed.total}
        noSprint={unthemed.noSprint}
        mode={mode}
        totalTestId={ids.retroTotalCell}
        totalDataCell="total"
      />
      <Link
        data-testid={ids.retroUnthemedLink}
        to={routePath('retros', { key: projectKey }, { unthemed: 'true' })}
        className="inline-flex h-[var(--touch-target)] items-center justify-self-start rounded-control border border-edge bg-panel px-3 text-[length:var(--text-sm)] text-ink no-underline"
      >
        Give them themes
      </Link>
    </article>
  );
}
