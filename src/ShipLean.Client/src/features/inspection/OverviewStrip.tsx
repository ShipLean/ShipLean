import type { RunAxisDto, RunOverviewTurnDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';
import './page/runpage.css';

export interface OverviewStripProps {
  overview: RunOverviewTurnDto[];
  axis: RunAxisDto;
  /** The turn the axis is zoomed to; null is the whole run. */
  zoomTurn: number | null;
  onZoom: (turn: number | null) => void;
}

const pct = (n: number) => `${n}%`;

// The whole run as blocks, one per turn; clicking a turn zooms the axis to it (D18).
// `axis` is the whole run's: the zoom is drawn as a window over it.
export function OverviewStrip({ overview, zoomTurn, onZoom }: OverviewStripProps) {
  const zoomed = zoomTurn === null ? undefined : overview.find((t) => t.turn === zoomTurn);
  // The gaps between turns are idle time.
  const idle = overview.slice(1).map((t, i) => ({ from: overview[i].endPct, to: t.startPct }));
  return (
    <div
      className="ov-wrap"
      data-testid={ids.runOverview}
      data-turns={overview.length}
      data-zoom-turn={zoomed?.turn ?? ''}
    >
      <span className="ov-label">
        {zoomed === undefined ? (
          'Whole run · click a turn to zoom to it'
        ) : (
          <>
            Zoomed to Turn {zoomed.turn} ·{' '}
            <button type="button" data-testid={ids.runWholeRun} onClick={() => onZoom(null)}>
              Whole run
            </button>
          </>
        )}
      </span>
      <div className="ov">
        {idle
          .filter((g) => g.to > g.from)
          .map((g) => (
            <span
              key={g.from}
              className="ov-idle"
              style={{ left: pct(g.from), width: pct(g.to - g.from) }}
            />
          ))}
        {zoomed === undefined ? null : (
          <span
            className="ov-win"
            style={{ left: pct(zoomed.startPct), width: pct(zoomed.endPct - zoomed.startPct) }}
          />
        )}
        {overview.map((t) => (
          <button
            key={t.turn}
            type="button"
            className={`ov-turn${t.turn === zoomTurn ? ' on' : ''}`}
            data-testid={ids.runOverviewTurn}
            data-turn={t.turn}
            data-error={t.hasError}
            aria-pressed={t.turn === zoomTurn}
            title={`Zoom to Turn ${t.turn}`}
            style={{ left: pct(t.startPct), width: pct(Math.max(t.endPct - t.startPct, 1)) }}
            onClick={() => onZoom(t.turn === zoomTurn ? null : t.turn)}
          >
            {t.turn}
            {t.waits.map((w) => (
              <span
                key={w.startPct}
                className="ov-wait"
                data-testid={ids.runOverviewWait}
                style={{
                  left: pct(
                    ((w.startPct - t.startPct) / Math.max(t.endPct - t.startPct, 0.01)) * 100,
                  ),
                  width: pct(
                    ((w.endPct - w.startPct) / Math.max(t.endPct - t.startPct, 0.01)) * 100,
                  ),
                }}
              />
            ))}
            {t.hasError ? <span className="ov-err" /> : null}
          </button>
        ))}
      </div>
    </div>
  );
}
