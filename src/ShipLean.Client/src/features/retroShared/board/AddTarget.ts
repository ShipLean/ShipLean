export type AddTarget =
  | { kind: 'item'; workItemId: string }
  | { kind: 'sprint'; sprintId: string; about: { value: string; label: string }[] };
