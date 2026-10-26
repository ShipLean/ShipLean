import type { BacklogRowDto, BacklogViewDto } from '../../../api/views';
import type { ParentRefDto } from '../../../api/workItems';

/** Where the dropped row goes at once, before the server answers. */
export type TreePlacement = { before: string } | { under: string };

/** What a drop in the tree asks the server for. */
export interface TreeDrop {
  /** The new parent (null clears it), or undefined when the parent stays. */
  parentId: string | null | undefined;
  /** The new parent as the row carries it, with the same meaning as `parentId`. */
  parent: ParentRefDto | null | undefined;
  /** Put the item just above this one, or undefined when its place in the stack stays. */
  beforeItemId: string | undefined;
  /** Where the row goes at once: just above a story, or first under a group (right after the group row). */
  placement: TreePlacement;
  /** The depth the row has in its new place. */
  depth: number;
  /** The toast on success. */
  done: string;
}

const parentRef = (group: BacklogRowDto): ParentRefDto | null =>
  group.kind === 'group' && group.type !== null
    ? { id: group.id, key: group.key, type: group.type, title: group.title }
    : null;

/** The group row a story sits under: the nearest row above it that is one level shallower. */
function groupOf(rows: readonly BacklogRowDto[], story: BacklogRowDto): BacklogRowDto | null {
  for (let at = rows.findIndex((row) => row.id === story.id) - 1; at >= 0; at--) {
    if (rows[at].depth < story.depth) return rows[at].kind === 'item' ? null : rows[at];
  }
  return null;
}

/**
 * What dropping `active` on `over` means. A Feature (or any group) row makes that group the parent; the No parent group
 * clears it; a story row gives the dropped one the parent of the group that story sits under and puts it just above.
 * Null when nothing changes. Only item ids go into `beforeItemId`: group rows are not items.
 *
 * A story under a Removed Feature sits under the nearest live group, though its Parent is still the Removed Feature.
 * Dropping on it makes that live group the parent (the one the user sees), never the Removed Feature. The dragged
 * row's own parent is read from `parent`: a Removed Feature is not null.
 */
export function treeTarget(
  rows: readonly BacklogRowDto[],
  active: BacklogRowDto,
  over: BacklogRowDto,
): TreeDrop | null {
  if (over.id === active.id) return null;
  const current = active.parent?.id ?? null;
  if (over.kind === 'noParentGroup') {
    if (current === null) return null;
    return {
      parentId: null,
      parent: null,
      beforeItemId: undefined,
      placement: { under: over.id },
      depth: over.depth + 1,
      done: `${active.key} no longer has a parent`,
    };
  }
  if (over.kind === 'group') {
    if (current === over.id) return null;
    return {
      parentId: over.id,
      parent: parentRef(over),
      beforeItemId: undefined,
      placement: { under: over.id },
      depth: over.depth + 1,
      done: `${active.key} moved under ${over.title}`,
    };
  }
  const group = groupOf(rows, over);
  const parentId = group === null || group.kind === 'noParentGroup' ? null : group.id;
  const changed = parentId !== current;
  return {
    parentId: changed ? parentId : undefined,
    parent: changed ? (group === null ? null : parentRef(group)) : undefined,
    beforeItemId: over.id,
    placement: { before: over.id },
    depth: over.depth,
    done: changed
      ? `${active.key} moved under ${group?.kind === 'group' ? group.title : 'No parent'}`
      : `${active.key} reordered`,
  };
}

/**
 * The read with the dropped row in its new place and at its new depth, as the server will put it: just above the story
 * it was dropped on, or first under the group (right after the group row, whether or not the group has stories).
 */
export function placeRow(
  view: BacklogViewDto,
  itemId: string,
  drop: Pick<TreeDrop, 'placement' | 'depth'>,
): BacklogViewDto {
  const moved = view.rows.find((row) => row.id === itemId);
  if (moved === undefined) return view;
  const rest = view.rows.filter((row) => row.id !== itemId);
  const anchor = 'before' in drop.placement ? drop.placement.before : drop.placement.under;
  const at = rest.findIndex((row) => row.id === anchor);
  if (at < 0) return view;
  const place = 'before' in drop.placement ? at : at + 1;
  return {
    ...view,
    rows: [...rest.slice(0, place), { ...moved, depth: drop.depth }, ...rest.slice(place)],
  };
}

/** The read with the revision and parent the server answered for one row. */
export function withWritten(
  view: BacklogViewDto,
  itemId: string,
  revision: number,
  parent: ParentRefDto | null | undefined,
): BacklogViewDto {
  return {
    ...view,
    rows: view.rows.map((row) =>
      row.id === itemId
        ? { ...row, revision, parent: parent === undefined ? row.parent : parent }
        : row,
    ),
  };
}
