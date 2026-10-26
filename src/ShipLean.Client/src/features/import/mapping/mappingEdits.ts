import type {
  ImportMappingDto,
  ImportTypeTarget,
  JiraStatusCategory,
  StatusMappingDto,
} from '../../../api/import';
import type { StateDto } from '../../../api/projects';
import type { StateCategory, UserRef } from '../../../api/refs';

// Every edit returns a new mapping; the server's totals and warnings stay as read until the run.
export const typeLabels: Record<ImportTypeTarget, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User Story',
  bug: 'Bug',
  task: 'Task',
  skip: 'Do not import',
};

// A state the run creates takes its category from Jira's status category.
export const categoryOf = (jira: JiraStatusCategory): StateCategory =>
  jira === 'new' ? 'proposed' : jira === 'indeterminate' ? 'inProgress' : 'completed';

export const categoryLabels: Record<StateCategory, string> = {
  proposed: 'Proposed',
  inProgress: 'In progress',
  resolved: 'Resolved',
  completed: 'Completed',
  removed: 'Removed',
};

const mapStatus = (
  mapping: ImportMappingDto,
  jiraType: string,
  jiraStatus: string,
  change: (row: StatusMappingDto) => StatusMappingDto,
): ImportMappingDto => ({
  ...mapping,
  statuses: mapping.statuses.map((s) =>
    s.jiraType === jiraType && s.jiraStatus === jiraStatus ? change(s) : s,
  ),
});

const landIn = (row: StatusMappingDto, states: StateDto[]): StatusMappingDto => {
  const existing = states.find((s) => s.name === row.targetState);
  return existing
    ? { ...row, targetCategory: existing.category, createsState: false }
    : {
        ...row,
        targetState: row.jiraStatus,
        targetCategory: categoryOf(row.jiraCategory),
        createsState: true,
      };
};

/** Changing a type regroups its statuses: each keeps its state when the new type has one of that name, else it is created. */
export function setTypeTarget(
  mapping: ImportMappingDto,
  jiraType: string,
  target: ImportTypeTarget,
  states: StateDto[],
): ImportMappingDto {
  const ofType = states.filter((s) => s.type === target);
  return {
    ...mapping,
    issueTypes: mapping.issueTypes.map((t) => (t.jiraType === jiraType ? { ...t, target } : t)),
    statuses: mapping.statuses.map((s) => (s.jiraType === jiraType ? landIn(s, ofType) : s)),
  };
}

export const NEW_STATE = '__new';

export function setStatusState(
  mapping: ImportMappingDto,
  row: StatusMappingDto,
  choice: string,
  states: StateDto[],
): ImportMappingDto {
  return mapStatus(mapping, row.jiraType, row.jiraStatus, (s) =>
    choice === NEW_STATE
      ? {
          ...s,
          targetState: s.jiraStatus,
          targetCategory: categoryOf(s.jiraCategory),
          createsState: true,
        }
      : landIn({ ...s, targetState: choice }, states),
  );
}

export const setStatusCategory = (
  mapping: ImportMappingDto,
  row: StatusMappingDto,
  targetCategory: StateCategory,
) => mapStatus(mapping, row.jiraType, row.jiraStatus, (s) => ({ ...s, targetCategory }));

export const CREATE_USER = 'create';
export const JIRA_IMPORT_USER = 'jiraImportUser';

/** choice: create, jiraImportUser, or the ShipLean user the Jira person is the same as. */
export function setUser(
  mapping: ImportMappingDto,
  jiraAccountId: string,
  choice: string,
  people: UserRef[],
): ImportMappingDto {
  return {
    ...mapping,
    users: mapping.users.map((u) => {
      if (u.jiraAccountId !== jiraAccountId) return u;
      if (choice === CREATE_USER) return { ...u, resolution: 'create', user: null };
      if (choice === JIRA_IMPORT_USER) return { ...u, resolution: 'jiraImportUser', user: null };
      return { ...u, resolution: 'existing', user: people.find((p) => p.id === choice) ?? u.user };
    }),
  };
}

export const setStoryPointsField = (mapping: ImportMappingDto, storyPointsFieldId: string) => ({
  ...mapping,
  fields: { ...mapping.fields, storyPointsFieldId },
});

export type IncludeOption =
  'includeComponents' | 'includeComments' | 'includeAttachments' | 'includeChangelog';

export const setInclude = (mapping: ImportMappingDto, option: IncludeOption, on: boolean) => ({
  ...mapping,
  fields: { ...mapping.fields, [option]: on },
});
