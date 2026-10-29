/** What a Taskboard grid is scoped to: one sprint (the Taskboard) or one Feature (the Feature board). */
export type TaskboardScope =
  { kind: 'sprint'; sprintId: string } | { kind: 'feature'; featureId: string };

/** The id of the sprint or Feature, for keys. */
export const scopeId = (scope: TaskboardScope): string =>
  scope.kind === 'sprint' ? scope.sprintId : scope.featureId;
