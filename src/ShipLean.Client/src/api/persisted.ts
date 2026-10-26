// The JSON documents ShipLean stores and the field-id vocabulary. C#: ShipLean.Contracts.Persisted.
// Property names are camelCase and nothing is omitted: absent values are null and lists are [].
import type { WorkItemTypeId } from './refs';

export const FIELD_KINDS = [
  'text',
  'markdown',
  'number',
  'points',
  'user',
  'state',
  'stateCategory',
  'type',
  'sprint',
  'workItem',
  'tags',
  'date',
  'checklist',
  'links',
] as const;
export type FieldKind = (typeof FIELD_KINDS)[number];

export const FIELD_USES = ['criteria', 'cardField', 'history'] as const;
export type FieldUse = (typeof FIELD_USES)[number];

export const CRITERIA_MATCHES = ['all', 'any'] as const;
export type CriteriaMatch = (typeof CRITERIA_MATCHES)[number];

export const CRITERIA_OPS = [
  '=',
  '<>',
  '>',
  '<',
  'in',
  'contains',
  'notContains',
  'isEmpty',
  'isNotEmpty',
] as const;
export type CriteriaOp = (typeof CRITERIA_OPS)[number];

export const SWIMLANE_KINDS = ['criteria', 'manual', 'default'] as const;
export type SwimlaneKind = (typeof SWIMLANE_KINDS)[number];

export const SWATCHES = ['danger', 'warn', 'ok', 'info', 'accent', 'muted', 'purple'] as const;
export type Swatch = (typeof SWATCHES)[number];

export const CARD_REORDERINGS = ['changesBacklogOrder', 'keepsBacklogOrder'] as const;
export type CardReordering = (typeof CARD_REORDERINGS)[number];

export const LINK_TYPES = [
  'predecessor',
  'successor',
  'blockedBy',
  'related',
  'duplicate',
  'url',
] as const;
export type LinkType = (typeof LINK_TYPES)[number];

export const IMPORT_JOB_STATES = [
  'queued',
  'running',
  'done',
  'doneWithErrors',
  'stopped',
  'failed',
] as const;
export type ImportJobState = (typeof IMPORT_JOB_STATES)[number];

export const IMPORT_STEPS = [
  'project',
  'states',
  'users',
  'sprints',
  'issues',
  'parents',
  'links',
  'comments',
  'attachments',
  'revisions',
  'finishing',
] as const;
export type ImportStep = (typeof IMPORT_STEPS)[number];

/** Criteria clause value: a scalar, a list for \`in\` (a null element matches an empty field), or null for isEmpty / isNotEmpty. */
export type CriteriaValue = string | number | (string | number | null)[] | null;

export interface CriteriaClause {
  field: string;
  op: CriteriaOp;
  value: CriteriaValue;
}

export interface CriteriaJson {
  match: CriteriaMatch;
  /** At least one. No criteria is a null CriteriaJson. */
  clauses: CriteriaClause[];
}

export interface ColumnStateMapping {
  type: WorkItemTypeId;
  stateId: string;
}

export interface BoardColumnJson {
  id: string;
  name: string;
  wipLimit: number | null;
  split: boolean;
  definitionOfDone: string;
  stateMappings: ColumnStateMapping[];
}

export interface SwimlaneJson {
  id: string;
  name: string;
  colour: Swatch | null;
  kind: SwimlaneKind;
  criteria: CriteriaJson | null;
}

export interface CardFieldSettingJson {
  type: WorkItemTypeId;
  showKey: boolean;
  showAssignee: boolean;
  showTags: boolean;
  showPoints: boolean;
  showEmptyFields: boolean;
  extraFields: string[];
}

export interface StyleRuleJson {
  id: string;
  name: string;
  enabled: boolean;
  colour: Swatch | null;
  bold: boolean;
  italic: boolean;
  underline: boolean;
  criteria: CriteriaJson;
}

export interface TagColourJson {
  tag: string;
  colour: Swatch;
}

export interface CardAnnotationsJson {
  tasks: boolean;
  stories: boolean;
  bugs: boolean;
  features: boolean;
}

export interface BoardSettingsJson {
  columns: BoardColumnJson[];
  swimlanes: SwimlaneJson[];
  cardFields: CardFieldSettingJson[];
  styleRules: StyleRuleJson[];
  tagColours: TagColourJson[];
  annotations: CardAnnotationsJson;
  cardReordering: CardReordering | null;
}

export interface ChecklistItem {
  text: string;
  done: boolean;
}

export interface LinkValue {
  type: LinkType;
  workItemId: string | null;
  key: string | null;
  url: string | null;
  title: string | null;
}

/** A field value by kind: text, markdown, user, state, sprint, workItem, date, type and stateCategory are strings. */
export type FieldValue = string | number | null | string[] | ChecklistItem[] | LinkValue[];

export interface FieldChange {
  field: string;
  from: FieldValue;
  to: FieldValue;
  fromLabel: string | null;
  toLabel: string | null;
}

export interface ImportEntityCount {
  done: number;
  total: number;
  failed: number;
}

export interface ImportCounts {
  sprints: ImportEntityCount;
  issues: ImportEntityCount;
  comments: ImportEntityCount;
  attachments: ImportEntityCount;
  revisions: ImportEntityCount;
}

export interface ImportRerunResult {
  updated: number;
  unchanged: number;
  added: number;
}

export interface ImportJobProgressJson {
  percent: number;
  step: ImportStep | null;
  message: string;
  rerun: boolean;
  counts: ImportCounts;
  usersMatched: number;
  usersCreated: number;
  statesCreated: number;
  rerunResult: ImportRerunResult | null;
}

export interface FieldValueOption {
  value: string | number;
  label: string;
}

export interface FieldDefinition {
  id: string;
  label: string;
  kind: FieldKind;
  appliesTo: readonly WorkItemTypeId[];
  nullable: boolean;
  usedIn: readonly FieldUse[];
  ops: readonly CriteriaOp[];
  /** Fixed value set with display labels; null is an open set. */
  values: readonly FieldValueOption[] | null;
}

/** The field-id vocabulary in canonical field order. Pinned to field-ids.json by a test. */
export const FIELD_IDS = [
  {
    id: 'id',
    label: 'ID',
    kind: 'workItem',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria'],
    ops: ['=', '<>', 'in'],
    values: null,
  },
  {
    id: 'key',
    label: 'Key',
    kind: 'text',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria'],
    ops: ['=', '<>', 'contains', 'notContains'],
    values: null,
  },
  {
    id: 'title',
    label: 'Title',
    kind: 'text',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'history'],
    ops: ['=', '<>', 'contains', 'notContains'],
    values: null,
  },
  {
    id: 'type',
    label: 'Work item type',
    kind: 'type',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'history'],
    ops: ['=', '<>', 'in'],
    values: [
      { value: 'epic', label: 'Epic' },
      { value: 'feature', label: 'Feature' },
      { value: 'story', label: 'User Story' },
      { value: 'bug', label: 'Bug' },
      { value: 'task', label: 'Task' },
    ],
  },
  {
    id: 'state',
    label: 'State',
    kind: 'state',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'cardField', 'history'],
    ops: ['=', '<>', 'in'],
    values: null,
  },
  {
    id: 'stateCategory',
    label: 'State category',
    kind: 'stateCategory',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria'],
    ops: ['=', '<>', 'in'],
    values: [
      { value: 'proposed', label: 'Proposed' },
      { value: 'inProgress', label: 'In Progress' },
      { value: 'resolved', label: 'Resolved' },
      { value: 'completed', label: 'Completed' },
      { value: 'removed', label: 'Removed' },
    ],
  },
  {
    id: 'assignee',
    label: 'Assigned to',
    kind: 'user',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria', 'history'],
    ops: ['=', '<>', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'priority',
    label: 'Priority',
    kind: 'number',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'cardField', 'history'],
    ops: ['=', '<>', '>', '<', 'in'],
    values: [
      { value: 1, label: '1' },
      { value: 2, label: '2' },
      { value: 3, label: '3' },
      { value: 4, label: '4' },
    ],
  },
  {
    id: 'points',
    label: 'Story points',
    kind: 'points',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria', 'history'],
    ops: ['=', '<>', '>', '<', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'tags',
    label: 'Tags',
    kind: 'tags',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'history'],
    ops: ['contains', 'notContains', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'sprint',
    label: 'Sprint',
    kind: 'sprint',
    appliesTo: ['story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria', 'cardField', 'history'],
    ops: ['=', '<>', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'parent',
    label: 'Parent',
    kind: 'workItem',
    appliesTo: ['feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria', 'cardField', 'history'],
    ops: ['=', '<>', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'epic',
    label: 'Epic',
    kind: 'workItem',
    appliesTo: ['feature'],
    nullable: true,
    usedIn: ['criteria'],
    ops: ['=', '<>', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'severity',
    label: 'Severity',
    kind: 'number',
    appliesTo: ['bug'],
    nullable: true,
    usedIn: ['criteria', 'cardField', 'history'],
    ops: ['=', '<>', '>', '<', 'in', 'isEmpty', 'isNotEmpty'],
    values: [
      { value: 1, label: '1 - Critical' },
      { value: 2, label: '2 - High' },
      { value: 3, label: '3 - Medium' },
      { value: 4, label: '4 - Low' },
    ],
  },
  {
    id: 'description',
    label: 'Description',
    kind: 'markdown',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'acceptanceCriteria',
    label: 'Acceptance criteria',
    kind: 'checklist',
    appliesTo: ['story'],
    nullable: false,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'reproSteps',
    label: 'Repro steps',
    kind: 'markdown',
    appliesTo: ['bug'],
    nullable: true,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'goal',
    label: 'Goal',
    kind: 'text',
    appliesTo: ['feature'],
    nullable: true,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'statusLine',
    label: 'Status',
    kind: 'text',
    appliesTo: ['feature'],
    nullable: true,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'statusVerifiedAt',
    label: 'Status verified',
    kind: 'date',
    appliesTo: ['feature'],
    nullable: true,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'links',
    label: 'Links',
    kind: 'links',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['history'],
    ops: [],
    values: null,
  },
  {
    id: 'createdAt',
    label: 'Created',
    kind: 'date',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'cardField'],
    ops: ['>', '<'],
    values: null,
  },
  {
    id: 'createdBy',
    label: 'Created by',
    kind: 'user',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria', 'cardField'],
    ops: ['=', '<>', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'changedAt',
    label: 'Changed',
    kind: 'date',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: false,
    usedIn: ['criteria', 'cardField'],
    ops: ['>', '<'],
    values: null,
  },
  {
    id: 'changedBy',
    label: 'Changed by',
    kind: 'user',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria', 'cardField'],
    ops: ['=', '<>', 'in', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'originalSystem',
    label: 'Original system',
    kind: 'text',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria'],
    ops: ['=', '<>', 'isEmpty', 'isNotEmpty'],
    values: [{ value: 'jira', label: 'Jira' }],
  },
  {
    id: 'originalSystemId',
    label: 'Original system ID',
    kind: 'text',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: ['criteria'],
    ops: ['=', '<>', 'contains', 'notContains', 'isEmpty', 'isNotEmpty'],
    values: null,
  },
  {
    id: 'originalUrl',
    label: 'Original URL',
    kind: 'text',
    appliesTo: ['epic', 'feature', 'story', 'bug', 'task'],
    nullable: true,
    usedIn: [],
    ops: [],
    values: null,
  },
] as const satisfies readonly FieldDefinition[];

export type FieldId = (typeof FIELD_IDS)[number]['id'];
