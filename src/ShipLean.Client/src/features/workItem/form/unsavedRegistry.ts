export type GuardScope = 'page' | 'modal';

// The work item forms that have unsaved changes, by form and scope. The one UnsavedGuardHost reads it when a navigation
// starts; nothing re-renders from it.
const dirtyForms = new Map<string, GuardScope>();

export function setFormDirty(id: string, scope: GuardScope, dirty: boolean) {
  if (dirty) dirtyForms.set(id, scope);
  else dirtyForms.delete(id);
}

export function hasDirtyForm(scope: GuardScope): boolean {
  return [...dirtyForms.values()].includes(scope);
}
