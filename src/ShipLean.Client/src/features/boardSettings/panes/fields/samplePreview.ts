import type { CardFieldSettingJson } from '../../../../api/persisted';
import type { ParentRefDto } from '../../../../api/workItems';
import type { TypeRef, WorkItemTypeId } from '../../../../api/refs';
import type { CardDto } from '../../../../api/views';
import { ALWAYS_DRAWN_FIELDS, fieldLabel } from './fieldEdits';

/** The project key the sample cards carry. */
export const PREVIEW_PROJECT_KEY = 'KEY';

const typeNames: Record<WorkItemTypeId, string> = {
  epic: 'Epic',
  feature: 'Feature',
  story: 'User story',
  bug: 'Bug',
  task: 'Task',
};

const typeRef = (id: WorkItemTypeId): TypeRef => ({ id, name: typeNames[id] });

const parentOf = (id: WorkItemTypeId, key: string, title: string): ParentRefDto => ({
  id: `parent-${key}`,
  key,
  type: typeRef(id),
  title,
});

/** The people and times the sample cards were made and changed by; rules read them as ids and ISO instants. */
export const SAMPLE_PEOPLE = {
  mei: { id: 'sample-mei', name: 'Mei Tanaka', initials: 'MT', avatarColour: '#0d9488' },
  tom: { id: 'sample-tom', name: 'Tom Reyes', initials: 'TR', avatarColour: '#7c3aed' },
};
export const SAMPLE_CREATED_AT = '2026-10-01T09:00:00Z';
export const SAMPLE_CHANGED_AT = '2026-10-14T15:30:00Z';

/** What the rules read on a sample card besides what the card carries: priority, who and when it was made and changed, severity (bugs have one). */
export function sampleRuleValues(type: WorkItemTypeId, priority: number): Record<string, unknown> {
  return {
    priority,
    severity: type === 'bug' ? 2 : null,
    sprint: null,
    createdAt: SAMPLE_CREATED_AT,
    createdBy: SAMPLE_PEOPLE.mei.id,
    changedAt: SAMPLE_CHANGED_AT,
    changedBy: SAMPLE_PEOPLE.tom.id,
  };
}

const common: Record<string, string | null> = {
  priority: '2',
  sprint: null,
  createdBy: 'Mei Tanaka',
  createdAt: '12 days ago',
  changedBy: 'Tom Reyes',
  changedAt: '2 days ago',
};

interface PreviewSample {
  key: string;
  title: string;
  points: number | null;
  childPoints: number | null;
  tags: string[];
  parent: ParentRefDto | null;
  fields: Record<string, string | null>;
}

/** One made-up card per type (nobody assigned, no sprint) so the gaps show when empty fields are turned on. */
const samples: Record<WorkItemTypeId, PreviewSample> = {
  epic: {
    key: 'KEY-1',
    title: 'Checkout rebuild',
    points: 40,
    childPoints: 46,
    tags: ['Checkout'],
    parent: null,
    fields: common,
  },
  feature: {
    key: 'KEY-12',
    title: 'Saved payment methods',
    points: 20,
    childPoints: 26,
    tags: ['Checkout'],
    parent: parentOf('epic', 'KEY-1', 'Checkout rebuild'),
    fields: common,
  },
  story: {
    key: 'KEY-58',
    title: 'Show delivery estimate in the basket',
    points: null,
    childPoints: null,
    tags: ['Feedback'],
    parent: parentOf('feature', 'KEY-12', 'Saved payment methods'),
    fields: common,
  },
  bug: {
    key: 'KEY-60',
    title: 'Basket total wrong after removing a promo',
    points: null,
    childPoints: null,
    tags: ['Customer'],
    parent: parentOf('feature', 'KEY-12', 'Saved payment methods'),
    fields: { ...common, severity: '2 - High' },
  },
  task: {
    key: 'KEY-61',
    title: 'Add estimate to the basket view model',
    points: null,
    childPoints: null,
    tags: [],
    parent: parentOf('story', 'KEY-58', 'Show delivery estimate in the basket'),
    fields: common,
  },
};

/**
 * The sample card of the setting's type as the board's server would send it for these settings: the extra
 * fields the card builder draws (not parent or state), an empty one only when empty fields are on.
 */
export function previewCard(setting: CardFieldSettingJson): CardDto {
  const sample = samples[setting.type];
  return {
    id: `preview-${sample.key}`,
    key: sample.key,
    type: typeRef(setting.type),
    title: sample.title,
    state: { id: 'preview-state', name: 'Active', category: 'inProgress' },
    assignee: null,
    points: sample.points,
    childPoints: sample.childPoints,
    sumWarn:
      sample.childPoints !== null &&
      sample.points !== null &&
      sample.childPoints > sample.points * 1.25,
    parent: sample.parent,
    tags: sample.tags.map((name) => ({ name, colour: null })),
    rollups: [],
    offerDone: false,
    styleRuleId: null,
    extraFields: setting.extraFields
      .filter((id) => !(id in ALWAYS_DRAWN_FIELDS))
      .map((id) => ({ fieldId: id, label: fieldLabel(id), text: sample.fields[id] ?? null }))
      .filter((field) => field.text !== null || setting.showEmptyFields),
    revision: 1,
  };
}
