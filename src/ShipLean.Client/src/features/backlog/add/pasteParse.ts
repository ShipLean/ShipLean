import type { WorkItemTypeId } from '@shiplean/core/api/refs';

/** One pasted line, as the review grid shows it. A task belongs to the nearest item above it. */
export interface PasteRow {
  /** The line's place among the lines kept, stable while rows are left out. */
  id: number;
  kind: 'item' | 'task';
  type: WorkItemTypeId;
  title: string;
  /** What was typed or pasted; a number when it is valid (see `pointsError`). */
  points: string;
  /** An indented line with no item above it, made an item. */
  promoted: boolean;
}

const COLUMNS = /\t| {2,}/;
const POINTS = /^\d+(\.\d+)?$/;
const INLINE_POINTS = /\(\s*(\d+(?:\.\d+)?)\s*pts?\s*\)\s*$/i;

/** Null when `points` is empty or a number; otherwise the reason. */
export function pointsError(points: string): string | null {
  const text = points.trim();
  return text === '' || POINTS.test(text) ? null : 'Points must be a number';
}

/** The number a valid points text stands for, null when it is empty. */
export function pointsValue(points: string): number | null {
  const text = points.trim();
  return POINTS.test(text) ? Number(text) : null;
}

/** How many non-blank lines a paste has. */
export function lineCount(text: string): number {
  return text.split(/\r?\n/).filter((line) => line.trim() !== '').length;
}

/**
 * Reads pasted text into rows, in pasted order. Pure, and it keeps every non-blank line.
 * Columns are split by a tab or two spaces; the first is the title and the first non-empty one after it is the points.
 * `defaultType` is the type of an item with no `bug:` prefix.
 */
export function parsePaste(text: string, defaultType: WorkItemTypeId = 'story'): PasteRow[] {
  const rows: PasteRow[] = [];
  let haveItem = false;
  for (const line of text.split(/\r?\n/)) {
    if (line.trim() === '') continue;
    const indented = /^(\t| {2,})/.test(line);
    const [first = '', ...rest] = line.trim().split(COLUMNS);
    let title = first.replace(/^[-*]\s+/, '').trim();
    let points = rest.find((cell) => cell.trim() !== '')?.trim() ?? '';
    const inline = INLINE_POINTS.exec(title);
    if (inline) {
      points = inline[1];
      title = title.slice(0, inline.index).trim();
    }
    const isTask = indented && haveItem;
    let type: WorkItemTypeId = 'task';
    if (!isTask) {
      type = defaultType;
      const bug = /^bug:\s*/i.exec(title);
      if (bug) {
        type = 'bug';
        title = title.slice(bug[0].length).trim();
      }
      haveItem = true;
    }
    rows.push({
      id: rows.length,
      kind: isTask ? 'task' : 'item',
      type,
      title,
      points,
      promoted: indented && !isTask,
    });
  }
  return rows;
}
