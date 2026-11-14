import { Link } from 'react-router-dom';
import type {
  RetroInsightColumnDto,
  RetroInsightsMode,
  RetroUnthemedDto,
} from '@shiplean/core/api/retros';
import { routePath } from '../../../../routes';
import { retrosTestIds as ids } from '../../testIds';
import { UnthemedTip } from '../tips/UnthemedTip';
import { HeatStrip } from './HeatStrip';

/** Points with no theme as the last, dashed card at touch (D1): its counts, "not judged", and the link to give them themes. */
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
  return (
    <article
      data-testid={ids.retroInsightsUnthemed}
      className="grid gap-2 rounded-panel border border-dashed border-edge bg-canvas px-3 py-2.5"
    >
      <header className="flex items-center gap-2">
        <span className="min-w-0 flex-1 text-[length:var(--text-base)] font-semibold italic">
          <UnthemedTip />
        </span>
        <span className="text-[length:var(--text-xs)] text-ink-muted">not judged</span>
      </header>
      <div className="grid grid-cols-[minmax(0,1fr)_52px] items-center">
        <HeatStrip counts={unthemed.counts} columns={columns} max={max} label="Unthemed" />
        <div className="grid justify-items-end leading-tight">
          <b
            data-testid={ids.retroTotalCell}
            data-cell="total"
            className="text-[length:var(--text-md)] tabular-nums"
          >
            {unthemed.total}
          </b>
          {mode === 'sprint' ? (
            <small className="text-[length:var(--text-2xs)] whitespace-nowrap text-ink-muted">
              {unthemed.noSprint} no sprint
            </small>
          ) : null}
        </div>
      </div>
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
