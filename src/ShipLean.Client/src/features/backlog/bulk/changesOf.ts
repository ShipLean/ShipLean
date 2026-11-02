import type { WorkItemChanges } from '../../../api/workItems';

/** A WorkItemChanges that changes nothing; spread the one field a bulk action sets over it. */
export function changesOf(set: Partial<WorkItemChanges>): WorkItemChanges {
  return {
    title: null,
    description: null,
    acceptanceCriteria: null,
    reproSteps: null,
    severity: null,
    state: null,
    assignee: null,
    sprint: null,
    priority: null,
    points: null,
    parent: null,
    tags: null,
    addLinks: [],
    removeLinkIds: [],
    ...set,
  };
}
