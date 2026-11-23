import { DURATION_LABEL_FONT_SIZE, textWidth, type LabelBox } from '../chart/labelBoxes';
import { DIAMOND_SIZE, MIN_BAR, type Mark } from '../chart/timelineGeometry';

// GanttMarks.tsx draws these with inline values it doesn't export; they are copied here and must match it.
/** The note GanttMarks.tsx writes beside a diamond. */
export const INFERRED_NOTE = 'Closed, no start recorded';
/** GanttMarks.tsx starts the note this far right of the diamond's right point. */
const INFERRED_NOTE_GAP = 5;
/** GanttMarks.tsx's OPEN_END_CUE: how far an open bar's end cue reaches past the bar's end. */
const OPEN_END_CUE = 5;
/** GanttMarks.tsx draws the end cue this far above and below the bar. */
const OPEN_END_CUE_OVERHANG = 2;

/** The box of the "Closed, no start recorded" note beside a diamond, at the note's font size. */
export function inferredNoteBox(mark: Mark): LabelBox {
  const cy = mark.box.y + mark.box.height / 2;
  const x0 = mark.box.x0 + DIAMOND_SIZE + INFERRED_NOTE_GAP;
  return {
    x0,
    x1: x0 + textWidth(INFERRED_NOTE, DURATION_LABEL_FONT_SIZE),
    y0: cy - DURATION_LABEL_FONT_SIZE / 2,
    y1: cy + DURATION_LABEL_FONT_SIZE / 2,
  };
}

/** The box of the end cue drawn past an open bar's end. */
export function openEndCueBox(mark: Mark): LabelBox {
  const end = mark.box.x0 + Math.max(mark.box.x1 - mark.box.x0, MIN_BAR);
  return {
    x0: end,
    x1: end + OPEN_END_CUE,
    y0: mark.box.y - OPEN_END_CUE_OVERHANG,
    y1: mark.box.y + mark.box.height + OPEN_END_CUE_OVERHANG,
  };
}

/**
 * What GanttMarks draws beside a mark outside its markBox and durationLabelBox: a diamond's note, and the end
 * cue of a bar that may be open. The layer isn't told which bars are open (GanttCanvas passes no bar states),
 * so every bar's cue spot is kept clear.
 */
export function markExtraBoxes(mark: Mark): LabelBox[] {
  if (mark.kind === 'diamond') return [inferredNoteBox(mark)];
  if (mark.kind === 'bar') return [openEndCueBox(mark)];
  return [];
}
