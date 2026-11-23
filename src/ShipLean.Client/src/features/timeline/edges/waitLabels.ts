import type { TimelineEdgeDto } from '@shiplean/core/api/views';
import { durationLabelBox, markBox, textWidth, type LabelBox } from '../chart/labelBoxes';
import { AXIS_HEIGHT, type Mark } from '../chart/timelineGeometry';
import { edgePairKey, type EdgePath } from './edgePaths';

/** Wait labels are drawn at this size, semibold, as the mock-up does. */
export const WAIT_LABEL_FONT_SIZE = 9.5;
/** The halo: a stroke this wide in the panel colour under the text. */
export const WAIT_LABEL_HALO = 3;
/** Text sits this far from the segment it labels. */
const SEGMENT_GAP = 3;
/** A label that doesn't fit centred slides along its segment in steps this long. */
const SLIDE_STEP = 8;

/** One placed wait label: its text is drawn centred on (x, y); box is the text and its halo. */
export interface WaitLabel {
  from: string;
  to: string;
  text: string;
  x: number;
  y: number;
  box: LabelBox;
}

const byKey = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

const overlaps = (a: LabelBox, b: LabelBox): boolean =>
  a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;

/** The label's box with its text centred on cx, the text's bottom at `bottom` or its top at `top`. */
function boxAt(cx: number, width: number, edge: { bottom: number } | { top: number }): LabelBox {
  const half = WAIT_LABEL_HALO / 2;
  const textTop = 'top' in edge ? edge.top : edge.bottom - WAIT_LABEL_FONT_SIZE;
  return {
    x0: cx - width / 2 - half,
    x1: cx + width / 2 + half,
    y0: textTop - half,
    y1: textTop + WAIT_LABEL_FONT_SIZE + half,
  };
}

/** Every spot to try, best first: above then below the horizontal segment, centred and then slid along it, then above and below the vertical one. */
function candidates(path: EdgePath, width: number): LabelBox[] {
  const { horizontal: h, vertical: v } = path;
  const above = { bottom: h.y - SEGMENT_GAP };
  const below = { top: h.y + SEGMENT_GAP };
  const centre = (h.x0 + h.x1) / 2;
  const spots = [boxAt(centre, width, above), boxAt(centre, width, below)];
  for (let step = SLIDE_STEP; step <= (h.x1 - h.x0) / 2; step += SLIDE_STEP) {
    for (const cx of [centre - step, centre + step]) {
      spots.push(boxAt(cx, width, above), boxAt(cx, width, below));
    }
  }
  spots.push(
    boxAt(v.x, width, { bottom: v.y0 - SEGMENT_GAP }),
    boxAt(v.x, width, { top: v.y1 + SEGMENT_GAP }),
  );
  return spots;
}

/**
 * Places each path's "{waitLabel} wait" label where it overlaps no mark, no duration label and no label placed
 * before it. Critical-path edges go first, then the longer waits, then by key. A label with no free spot is left
 * out (its arrow's title still says the wait); the result is in placement order.
 */
export function placeWaitLabels(
  edges: TimelineEdgeDto[],
  paths: EdgePath[],
  marks: Mark[],
): WaitLabel[] {
  // The first edge of a pair is the one drawn (edgePaths), so its wait ranks the label.
  const waitHours = new Map<string, number>();
  for (const e of edges) {
    if (!waitHours.has(edgePairKey(e))) waitHours.set(edgePairKey(e), e.waitHours ?? 0);
  }
  const taken: LabelBox[] = marks.flatMap((m) => {
    const label = m.durationLabel === null ? null : durationLabelBox(m, m.durationLabel);
    return label === null ? [markBox(m)] : [markBox(m), label];
  });
  const ordered = paths
    .filter((p) => p.waitLabel !== null)
    .map((p) => ({ path: p, hours: waitHours.get(edgePairKey(p)) ?? 0 }))
    .sort(
      (a, b) =>
        Number(b.path.onCriticalPath) - Number(a.path.onCriticalPath) ||
        b.hours - a.hours ||
        byKey(edgePairKey(a.path), edgePairKey(b.path)),
    );

  const placed: WaitLabel[] = [];
  for (const { path } of ordered) {
    const text = `${path.waitLabel} wait`;
    const width = textWidth(text, WAIT_LABEL_FONT_SIZE);
    const spot = candidates(path, width).find(
      (box) => box.x0 >= 0 && box.y0 >= AXIS_HEIGHT && !taken.some((t) => overlaps(box, t)),
    );
    if (!spot) continue;
    taken.push(spot);
    placed.push({
      from: path.from,
      to: path.to,
      text,
      x: (spot.x0 + spot.x1) / 2,
      y: (spot.y0 + spot.y1) / 2,
      box: spot,
    });
  }
  return placed;
}
