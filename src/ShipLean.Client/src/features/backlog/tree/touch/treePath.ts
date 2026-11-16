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

/**
 * The ancestors above a group row, for the muted first line of its header: "Epic › Feature ›", or '' when it has none
 * (an Epic, the No parent group). The group's own key and title go on the line under it.
 */
export function treeParents(rows: readonly BacklogRowDto[], row: BacklogRowDto): string {
  const titles = treePath(rows, row).split(' › ');
  return titles.length > 1 && row.kind !== 'noParentGroup'
    ? `${titles.slice(0, -1).join(' › ')} ›`
    : '';
}
