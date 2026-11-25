import { DIAMOND_SIZE, MIN_BAR, REVIEW_DOT_RADIUS, type Mark } from './timelineGeometry';

/** A box in the chart's pixels: x0 ≤ x1 and y0 ≤ y1. */
export interface LabelBox {
  x0: number;
  x1: number;
  y0: number;
  y1: number;
}

/** Duration labels are drawn at this size. */
export const DURATION_LABEL_FONT_SIZE = 10;
/** The gap between a mark's end (or its last review dot) and its duration label. */
export const DURATION_LABEL_GAP = 6;
/** An average character is this share of the font size wide; a fixed estimate, so no DOM is measured. */
const CHAR_WIDTH = 0.6;

export function textWidth(text: string, fontSize: number): number {
  return text.length * fontSize * CHAR_WIDTH;
}

const middle = (mark: Mark): number => mark.box.y + mark.box.height / 2;

/** Where the drawn bar or bracket ends: never narrower than MIN_BAR. */
const drawnEnd = (mark: Mark): number => Math.max(mark.box.x1, mark.box.x0 + MIN_BAR);

/** The right edge of the last review dot, or null when there are none. */
const lastDotEdge = (mark: Mark): number | null =>
  mark.reviews.length === 0
    ? null
    : Math.max(...mark.reviews.map((dot) => dot.x)) + REVIEW_DOT_RADIUS;

/** The mark as drawn, review dots included; a diamond is its square. */
export function markBox(mark: Mark): LabelBox {
  const cy = middle(mark);
  const shape =
    mark.kind === 'diamond'
      ? {
          x0: mark.box.x0 - DIAMOND_SIZE,
          x1: mark.box.x0 + DIAMOND_SIZE,
          y0: cy - DIAMOND_SIZE,
          y1: cy + DIAMOND_SIZE,
        }
      : { x0: mark.box.x0, x1: drawnEnd(mark), y0: mark.box.y, y1: mark.box.y + mark.box.height };
  return mark.reviews.reduce(
    (box, dot) => ({
      x0: Math.min(box.x0, dot.x - REVIEW_DOT_RADIUS),
      x1: Math.max(box.x1, dot.x + REVIEW_DOT_RADIUS),
      y0: Math.min(box.y0, cy - REVIEW_DOT_RADIUS),
      y1: Math.max(box.y1, cy + REVIEW_DOT_RADIUS),
    }),
    shape,
  );
}

/**
 * Where a bar's or bracket's duration label sits: DURATION_LABEL_GAP right of the later of the mark's end
 * and its last review dot, vertically centred on the mark. A diamond has no duration label.
 */
export function durationLabelBox(mark: Mark, label: string): LabelBox | null {
  if (mark.kind === 'diamond') return null;
  const end = Math.max(drawnEnd(mark), lastDotEdge(mark) ?? -Infinity);
  const x0 = end + DURATION_LABEL_GAP;
  const cy = middle(mark);
  return {
    x0,
    x1: x0 + textWidth(label, DURATION_LABEL_FONT_SIZE),
    y0: cy - DURATION_LABEL_FONT_SIZE / 2,
    y1: cy + DURATION_LABEL_FONT_SIZE / 2,
  };
}
