import { expect, test } from 'vitest';
import events from '../../../ShipLean.TestSupport/DtoFixtures/events/events.json';
import { routeIds } from '../routes';
import {
  PROJECT_EVENT_TYPES,
  type BoardSettingsChanged,
  type ImportProgress,
  type CommentsChanged,
  type AttachmentsChanged,
  type LinksChanged,
  type AgentSessionChanged,
  type PresenceChanged,
  type ProjectChanged,
  type ProjectEvent,
  type SprintChanged,
  type StatesChanged,
  type WorkItemCreated,
  type WorkItemMoved,
  type WorkItemRemoved,
  type WorkItemReordered,
  type WorkItemUpdated,
} from './events';
import { BOARD_SECTIONS } from './boards';
import { IMPORT_JOB_STATES } from './persisted';
import { WORK_ITEM_TYPE_IDS } from './refs';

// Literal unions widened to their base type, recursively: what a JSON import looks like.
type DeepWiden<T> = T extends string
  ? string
  : T extends number
    ? number
    : T extends boolean
      ? boolean
      : T extends readonly (infer U)[]
        ? DeepWiden<U>[]
        : T extends object
          ? { [K in keyof T]: DeepWiden<T[K]> }
          : T;

// Maps every key the target type lacks to never, recursively, so a fixture property the type has dropped fails the check.
type NoExtraKeys<U, T> = U extends readonly (infer UE)[]
  ? T extends readonly (infer TE)[]
    ? NoExtraKeys<UE, TE>[]
    : U
  : U extends object
    ? T extends object
      ? { [K in keyof U]: K extends keyof T ? NoExtraKeys<U[K], T[K]> : never }
      : U
    : U;

// Typecheck fails if the fixture lacks a field, has one of the wrong type, or has one the type doesn't declare.
function typed<T>() {
  return <U extends DeepWiden<T>>(value: U & NoExtraKeys<U, DeepWiden<T>>): DeepWiden<T> => value;
}

const examples = {
  workItemCreated: typed<WorkItemCreated>()(events.workItemCreated),
  workItemUpdated: typed<WorkItemUpdated>()(events.workItemUpdated),
  workItemMoved: typed<WorkItemMoved>()(events.workItemMoved),
  workItemReordered: typed<WorkItemReordered>()(events.workItemReordered),
  workItemRemoved: typed<WorkItemRemoved>()(events.workItemRemoved),
  boardSettingsChanged: typed<BoardSettingsChanged>()(events.boardSettingsChanged),
  sprintChanged: typed<SprintChanged>()(events.sprintChanged),
  statesChanged: typed<StatesChanged>()(events.statesChanged),
  projectChanged: typed<ProjectChanged>()(events.projectChanged),
  presenceChanged: typed<PresenceChanged>()(events.presenceChanged),
  importProgress: typed<ImportProgress>()(events.importProgress),
  commentsChanged: typed<CommentsChanged>()(events.commentsChanged),
  attachmentsChanged: typed<AttachmentsChanged>()(events.attachmentsChanged),
  linksChanged: typed<LinksChanged>()(events.linksChanged),
  agentSessionChanged: typed<AgentSessionChanged>()(events.agentSessionChanged),
};

// Compile-time: every union member is covered by a case, so a new event type fails the typecheck until it is handled.
function typeOf(event: ProjectEvent): string {
  switch (event.type) {
    case 'workItemCreated':
    case 'workItemUpdated':
    case 'workItemMoved':
    case 'workItemReordered':
    case 'workItemRemoved':
    case 'boardSettingsChanged':
    case 'sprintChanged':
    case 'statesChanged':
    case 'projectChanged':
    case 'presenceChanged':
    case 'importProgress':
    case 'commentsChanged':
    case 'attachmentsChanged':
    case 'linksChanged':
    case 'agentSessionChanged':
      return event.type;
  }
}

test('projectEvents_ShouldTypeEveryFixture', () => {
  for (const [name, example] of Object.entries(examples)) {
    expect(example.type).toBe(name);
    expect(PROJECT_EVENT_TYPES).toContain(example.type);
  }
  expect(Object.keys(examples)).toHaveLength(PROJECT_EVENT_TYPES.length);
  expect(WORK_ITEM_TYPE_IDS).toContain(examples.workItemCreated.workItemType);
  expect(IMPORT_JOB_STATES).toContain(examples.importProgress.state);
  expect(routeIds).toContain(examples.presenceChanged.screenId);
  for (const section of examples.boardSettingsChanged.sections) {
    expect(BOARD_SECTIONS).toContain(section);
  }
  expect(typeOf(events.workItemRemoved as ProjectEvent)).toBe('workItemRemoved');
});

test('EventTypes_ShouldMatchBetweenCSharpAndTypeScript', () => {
  expect([...PROJECT_EVENT_TYPES].sort()).toEqual(Object.keys(events).sort());
});
