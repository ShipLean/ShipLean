import type { RunAxisDto, RunOverviewTurnDto } from '@shiplean/core/api/inspection';
import { inspectionTestIds as ids } from './testIds';

export interface OverviewStripProps {
  overview: RunOverviewTurnDto[];
  axis: RunAxisDto;
  /** The turn the axis is zoomed to; null is the whole run. */
  zoomTurn: number | null;
  onZoom: (turn: number | null) => void;
}

// The whole run as blocks, one per turn; clicking a turn zooms the axis to it (D18).
// TODO: the strip replaces this stub.
export function OverviewStrip({ overview, zoomTurn }: OverviewStripProps) {
  return (
    <div
      data-testid={ids.runOverview}
      data-turns={overview.length}
      data-zoom-turn={zoomTurn ?? ''}
    />
  );
}
