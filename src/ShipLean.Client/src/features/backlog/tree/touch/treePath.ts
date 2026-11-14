import type { BacklogRowDto } from '@shiplean/core/api/views';

/**
 * The path a group row sits at, as the touch tree's header names it: "Epic › Feature › Nested". The parents are the
 * nearest rows above it that are one level shallower each; the No parent group is nobody's parent and has just its own
 * title. The whole path is shown, never cut.
 */
export function treePath(rows: readonly BacklogRowDto[], row: BacklogRowDto): string {
  const titles = [row.title];
  let depth = row.depth;
  for (let at = rows.findIndex((r) => r.id === row.id) - 1; at >= 0 && depth > 0; at--) {
    const above = rows[at];
    if (above.depth < depth && above.kind !== 'noParentGroup') {
      titles.unshift(above.title);
      depth = above.depth;
    }
  }
  return titles.join(' › ');
}
