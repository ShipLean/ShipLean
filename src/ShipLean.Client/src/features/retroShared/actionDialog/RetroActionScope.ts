/** What a note or board sits in; the action dialog uses it to scope the points it offers. */
export type RetroActionScope =
  { kind: 'item'; itemId: string } | { kind: 'sprint'; sprintId: string } | { kind: 'project' };
