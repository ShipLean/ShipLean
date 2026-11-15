/** What `boardRowIds` reads: the story rows and the people rows of a Taskboard or Feature board view. */
export interface BoardRowIdsView {
  rows: { parent: { id: string } | null }[];
  peopleRows: { person: { id: string } | null }[];
}

/** Every row's id as the grids key their collapse state: a story by its id (`unparented`), a person by theirs (`unassigned`). */
export function boardRowIds(view: BoardRowIdsView): string[] {
  return [
    ...view.rows.map((row) => row.parent?.id ?? 'unparented'),
    ...view.peopleRows.map((row) => row.person?.id ?? 'unassigned'),
  ];
}

/** A collapse map with every row set to `collapsed`, for "Collapse all" and "Expand all". */
export function collapseAllMap(
  view: BoardRowIdsView,
  collapsed: boolean,
): ReadonlyMap<string, boolean> {
  return new Map(boardRowIds(view).map((id) => [id, collapsed]));
}
